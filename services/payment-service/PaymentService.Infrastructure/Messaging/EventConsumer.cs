using System.Text.Json;
using CargoService.Contracts.Events.V1;
using CargoService.Contracts.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PaymentService.Infrastructure.Messaging;

/// <summary>
/// Обобщённый консьюмер одного события: своя очередь <c>payment-service.{event}</c> с DLQ,
/// переподключение при обрыве, разбор payload, отсев уже обработанных событий (inbox) и
/// передача в <see cref="IEventHandler{TEvent}"/>.
/// <para>
/// Взят в том же виде, что в document-service и notification-service. Подписок здесь всего две
/// (<c>OrderConfirmed</c> и <c>OrderCancelled</c>), и написать два отдельных
/// <c>BackgroundService</c> было бы не сильно длиннее — но топология очередей у них одинаковая,
/// и две её копии разошлись бы при первой же правке.
/// </para>
/// </summary>
public class EventConsumer<TEvent>(
    IServiceScopeFactory scopeFactory,
    RabbitMqOptions options,
    string publishingService,
    ILogger<EventConsumer<TEvent>> logger) : BackgroundService
    where TEvent : IntegrationEvent
{
    private const string ConsumingService = "payment-service";
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
            var inbox = scope.ServiceProvider.GetRequiredService<IInboxRepository>();

            if (await inbox.IsProcessedAsync(@event.EventId, stoppingToken))
            {
                logger.LogInformation(
                    "{EventName} {EventId} already processed, skipping duplicate delivery",
                    EventName, @event.EventId);
                await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, stoppingToken);
                return;
            }

            var handler = scope.ServiceProvider.GetRequiredService<IEventHandler<TEvent>>();
            await handler.HandleAsync(@event, stoppingToken);

            // Отметка ставится ПОСЛЕ обработки: обратный порядок («забронировали и обрабатываем»)
            // терял бы счёт насовсем, упади сервис между отметкой и записью, — заявка осталась бы
            // неоплачиваемой, и никто бы об этом не узнал. Здесь же худший случай — повторная
            // обработка, а от неё защищает уникальность счёта по заявке (и проверка в сценарии).
            if (!await inbox.TryMarkProcessedAsync(@event.EventId, EventName, stoppingToken))
            {
                logger.LogWarning(
                    "{EventName} {EventId} was processed concurrently",
                    EventName, @event.EventId);
            }

            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, stoppingToken);
        }
        catch (Exception ex)
        {
            // В DLQ уходит то, что действительно не разобрать: битый payload, отказ БД.
            logger.LogError(
                ex,
                "Failed to process {EventName} message {MessageId}, sending to dead-letter queue",
                EventName,
                eventArgs.BasicProperties.MessageId);
            await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, stoppingToken);
        }
    }
}
