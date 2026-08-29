using System.Text.Json;
using AiInspectionService.Application.Interfaces;
using CargoService.Contracts.Events.V1;
using CargoService.Contracts.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AiInspectionService.Infrastructure.Messaging;

/// <summary>
/// Слушает <c>CargoPhotoUploaded</c> от cargo-service и ставит задание на проверку снимков.
/// Сам инференс здесь не запускается — задание кладётся в очередь со статусом
/// <c>Queued</c>, а разбирает её фоновый конвейер (следующая задача Фазы 7): держать
/// обработку сообщения открытой на время инференса значило бы мерить таймауты брокера
/// скоростью модели.
/// </summary>
public class CargoPhotoUploadedConsumer(
    IServiceScopeFactory scopeFactory,
    RabbitMqOptions options,
    ILogger<CargoPhotoUploadedConsumer> logger) : BackgroundService
{
    private const string ConsumingService = "ai-inspection-service";
    private const string PublishingService = "cargo-service";
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);

    private static readonly string QueueName = RabbitMqConventions.QueueName(ConsumingService, nameof(CargoPhotoUploaded));
    private static readonly string DeadLetterQueueName = QueueName + RabbitMqConventions.DeadLetterSuffix;
    private static readonly string RoutingKey = RabbitMqConventions.RoutingKey(PublishingService, nameof(CargoPhotoUploaded));

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
                logger.LogError(ex, "CargoPhotoUploaded consumer failed, reconnecting in {Delay}", ReconnectDelay);
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

        await channel.QueueBindAsync(QueueName, RabbitMqConventions.EventsExchange, RoutingKey, cancellationToken: stoppingToken);
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
            var @event = JsonSerializer.Deserialize<CargoPhotoUploaded>(eventArgs.Body.Span)
                ?? throw new InvalidOperationException("CargoPhotoUploaded payload deserialized to null.");

            using var scope = scopeFactory.CreateScope();
            var inbox = scope.ServiceProvider.GetRequiredService<IInboxRepository>();

            // Схема повторные задания по одному грузу разрешает (ручной перезапуск — штатный
            // сценарий), поэтому дубли от at-least-once ловит именно inbox.
            if (await inbox.IsProcessedAsync(@event.EventId, stoppingToken))
            {
                logger.LogInformation(
                    "CargoPhotoUploaded {EventId} already processed, skipping duplicate delivery",
                    @event.EventId);
                await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, stoppingToken);
                return;
            }

            var jobsService = scope.ServiceProvider.GetRequiredService<IInspectionJobsService>();
            var job = await jobsService.EnqueueAsync(@event.ShipmentId, @event.PhotoFileIds, stoppingToken);

            if (job is not null)
            {
                logger.LogInformation(
                    "Queued inspection job {JobId} for shipment {TrackingNumber} ({PhotoCount} photos)",
                    job.Id, @event.TrackingNumber, job.PhotoFileIds.Count);
            }
            else
            {
                // Событие без снимков: проверять нечего, но это не сбой — cargo-service шлёт
                // событие только с новыми файлами, и пустой список означает лишь, что все они
                // уже были в акте.
                logger.LogInformation(
                    "CargoPhotoUploaded for shipment {TrackingNumber} has no photos, nothing to inspect",
                    @event.TrackingNumber);
            }

            // Отметка ставится после создания задания: обратный порядок терял бы проверку
            // насовсем, упади сервис между отметкой и записью, а этот порядок в худшем случае
            // заведёт второе задание — лишний инференс неприятен, но необследованный груз хуже.
            if (!await inbox.TryMarkProcessedAsync(@event.EventId, nameof(CargoPhotoUploaded), stoppingToken))
            {
                logger.LogWarning(
                    "CargoPhotoUploaded {EventId} was processed concurrently, a duplicate job may exist",
                    @event.EventId);
            }

            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to process CargoPhotoUploaded message {MessageId}, sending to dead-letter queue",
                eventArgs.BasicProperties.MessageId);
            await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, stoppingToken);
        }
    }
}
