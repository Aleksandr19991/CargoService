using System.Text.Json;
using CargoService.Application.Interfaces;
using CargoService.Contracts.Events.V1;
using CargoService.Contracts.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CargoService.Infrastructure.Messaging;

/// <summary>
/// Consumes OrderConfirmed events published by orders-service's outbox and creates the matching
/// Shipment with a freshly generated tracking number. Idempotent by construction —
/// IShipmentsService.CreateFromConfirmedOrderAsync is create-if-not-exists keyed on the unique
/// index on OrderId, so redelivery of the same event (RabbitMQ is at-least-once) is harmless and
/// no separate inbox/dedup table is needed.
/// </summary>
public class OrderConfirmedConsumer(
    IServiceScopeFactory scopeFactory,
    RabbitMqOptions options,
    ILogger<OrderConfirmedConsumer> logger) : BackgroundService
{
    private const string ConsumingService = "cargo-service";
    private const string PublishingService = "orders-service";
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);

    private static readonly string QueueName = RabbitMqConventions.QueueName(ConsumingService, nameof(OrderConfirmed));
    private static readonly string DeadLetterQueueName = QueueName + RabbitMqConventions.DeadLetterSuffix;
    private static readonly string RoutingKey = RabbitMqConventions.RoutingKey(PublishingService, nameof(OrderConfirmed));

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
                logger.LogError(ex, "OrderConfirmed consumer failed, reconnecting in {Delay}", ReconnectDelay);
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
            var orderConfirmed = JsonSerializer.Deserialize<OrderConfirmed>(eventArgs.Body.Span)
                ?? throw new InvalidOperationException("OrderConfirmed payload deserialized to null.");

            using var scope = scopeFactory.CreateScope();
            var shipmentsService = scope.ServiceProvider.GetRequiredService<IShipmentsService>();

            var shipment = await shipmentsService.CreateFromConfirmedOrderAsync(
                orderConfirmed.OrderId, orderConfirmed.DeliveryDeadline, stoppingToken);

            if (shipment is not null)
            {
                logger.LogInformation(
                    "Created shipment {ShipmentId} ({TrackingNumber}) for confirmed order {OrderNumber}",
                    shipment.Id, shipment.TrackingNumber, orderConfirmed.OrderNumber);
            }
            else
            {
                // Груз по этой заявке уже есть — повторная доставка события. Это штатный исход,
                // а не сбой: ack, без DLQ.
                logger.LogInformation(
                    "Shipment for order {OrderId} already exists, skipping duplicate OrderConfirmed",
                    orderConfirmed.OrderId);
            }

            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to process OrderConfirmed message {MessageId}, sending to dead-letter queue",
                eventArgs.BasicProperties.MessageId);
            await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, stoppingToken);
        }
    }
}
