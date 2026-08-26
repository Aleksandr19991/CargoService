using System.Text.Json;
using CargoService.Contracts.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService.Infrastructure.Messaging;

/// <summary>
/// Обобщённый консьюмер одного события: своя очередь <c>notification-service.{event}</c> с DLQ,
/// переподключение при обрыве, разбор payload и передача в <see cref="IEventHandler{TEvent}"/>.
/// <para>
/// В остальных сервисах на каждое событие пишется свой <c>BackgroundService</c>, но там их одно-
/// два; здесь событий десяток, и десять копий одной топологии были бы десятью местами, где она
/// может незаметно разойтись. Отличаются подписки только типом события и сервисом-издателем —
/// ровно они и вынесены в параметры.
/// </para>
/// </summary>
public class EventConsumer<TEvent>(
    IServiceScopeFactory scopeFactory,
    RabbitMqOptions options,
    string publishingService,
    ILogger<EventConsumer<TEvent>> logger) : BackgroundService
{
    private const string ConsumingService = "notification-service";
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);

    private static readonly string EventName = typeof(TEvent).Name;
    private static readonly string QueueName = RabbitMqConventions.QueueName(ConsumingService, EventName);
    private static readonly string DeadLetterQueueName = QueueName + RabbitMqConventions.DeadLetterSuffix;

    private readonly string routingKey = RabbitMqConventions.RoutingKey(publishingService, EventName);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down.
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{EventName} consumer failed, reconnecting in {Delay}", EventName, ReconnectDelay);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
        };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(
            RabbitMqConventions.EventsExchange,
            ExchangeType.Topic,
            durable: true,
            cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        // Default (nameless) exchange routes by queue name, so routing the dead letter straight
        // to the DLQ's name is enough — no separate dead-letter exchange to declare/bind.
        await channel.QueueDeclareAsync(
            QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = DeadLetterQueueName,
            },
            cancellationToken: stoppingToken);

        await channel.QueueBindAsync(QueueName, RabbitMqConventions.EventsExchange, routingKey, cancellationToken: stoppingToken);
        await channel.BasicQosAsync(0, prefetchCount: 10, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, eventArgs) => await HandleMessageAsync(channel, eventArgs, stoppingToken);

        await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, stoppingToken);

        // BasicConsumeAsync only registers the consumer — keep the connection/channel alive for
        // the rest of the app's lifetime (or until an exception from the callback bubbles up and
        // ExecuteAsync's catch-all reconnects).
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleMessageAsync(IChannel channel, BasicDeliverEventArgs eventArgs, CancellationToken stoppingToken)
    {
        try
        {
            var @event = JsonSerializer.Deserialize<TEvent>(eventArgs.Body.Span)
                ?? throw new InvalidOperationException($"{EventName} payload deserialized to null.");

            using var scope = scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<IEventHandler<TEvent>>();

            await handler.HandleAsync(@event, stoppingToken);

            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, stoppingToken);
        }
        catch (Exception ex)
        {
            // Неизвестный получатель или отсутствующий шаблон сюда не попадают — обработчик
            // считает их штатным исходом и логирует сам (см. NotificationsService). В DLQ
            // уходит то, что действительно не разобрать: битый payload, отказ БД.
            logger.LogError(
                ex,
                "Failed to process {EventName} message {MessageId}, sending to dead-letter queue",
                EventName,
                eventArgs.BasicProperties.MessageId);
            await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, stoppingToken);
        }
    }
}
