using System.Text.Json;
using CargoService.Application.Interfaces;
using CargoService.Application.Models;
using CargoService.Contracts.Events.V1;
using CargoService.Contracts.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CargoService.Infrastructure.Messaging;

/// <summary>
/// Consumes PackageIntegrityAssessed events published by ai-inspection-service and records the
/// verdict on the shipment's acceptance act, flagging a discrepancy with the human assessment.
/// Alerting the staff is notification-service's job — it subscribes to the same event.
/// </summary>
public class PackageIntegrityAssessedConsumer(
    IServiceScopeFactory scopeFactory,
    RabbitMqOptions options,
    ILogger<PackageIntegrityAssessedConsumer> logger) : BackgroundService
{
    private const string ConsumingService = "cargo-service";
    private const string PublishingService = "ai-inspection-service";
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);

    private static readonly string QueueName = RabbitMqConventions.QueueName(ConsumingService, nameof(PackageIntegrityAssessed));
    private static readonly string DeadLetterQueueName = QueueName + RabbitMqConventions.DeadLetterSuffix;
    private static readonly string RoutingKey = RabbitMqConventions.RoutingKey(PublishingService, nameof(PackageIntegrityAssessed));

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
                logger.LogError(ex, "PackageIntegrityAssessed consumer failed, reconnecting in {Delay}", ReconnectDelay);
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
            var assessed = JsonSerializer.Deserialize<PackageIntegrityAssessed>(eventArgs.Body.Span)
                ?? throw new InvalidOperationException("PackageIntegrityAssessed payload deserialized to null.");

            using var scope = scopeFactory.CreateScope();
            var shipmentsService = scope.ServiceProvider.GetRequiredService<IShipmentsService>();

            var assessment = new PackageIntegrityAssessment
            {
                InspectionJobId = assessed.InspectionJobId,
                DamageDetected = assessed.DamageDetected,
                Confidence = assessed.Confidence,
            };

            var applied = await shipmentsService.ApplyIntegrityAssessmentAsync(assessed.ShipmentId, assessment, stoppingToken);

            if (applied)
            {
                logger.LogInformation(
                    "Recorded AI integrity verdict for shipment {ShipmentId}: damage={DamageDetected}, confidence={Confidence}",
                    assessed.ShipmentId, assessed.DamageDetected, assessed.Confidence);
            }
            else
            {
                // Нет груза или он ещё не принят — приписать вердикт некуда. Это перманентное
                // несоответствие, а не временный сбой: ретрай через DLQ не помог бы.
                logger.LogWarning(
                    "PackageIntegrityAssessed referenced shipment {ShipmentId} with no acceptance act, dropping",
                    assessed.ShipmentId);
            }

            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to process PackageIntegrityAssessed message {MessageId}, sending to dead-letter queue",
                eventArgs.BasicProperties.MessageId);
            await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, stoppingToken);
        }
    }
}
