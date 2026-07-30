using System.Text.Json;
using CargoService.Contracts.Events.V1;
using CargoService.Contracts.Messaging;
using OrdersService.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrdersService.Infrastructure.Messaging;

/// <summary>
/// Consumes PaymentCompleted events published by payment-service's outbox and projects
/// IsPaid/PaymentId onto the matching Order row (read-model only — no further events are
/// published from here).
/// </summary>
public class PaymentCompletedConsumer(
    IServiceScopeFactory scopeFactory,
    RabbitMqOptions options,
    ILogger<PaymentCompletedConsumer> logger) : BackgroundService
{
    private const string ConsumingService = "orders-service";
    private const string PublishingService = "payment-service";
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);

    private static readonly string QueueName = RabbitMqConventions.QueueName(ConsumingService, nameof(PaymentCompleted));
    private static readonly string DeadLetterQueueName = QueueName + RabbitMqConventions.DeadLetterSuffix;
    private static readonly string RoutingKey = RabbitMqConventions.RoutingKey(PublishingService, nameof(PaymentCompleted));

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
                logger.LogError(ex, "PaymentCompleted consumer failed, reconnecting in {Delay}", ReconnectDelay);
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
            var paymentCompleted = JsonSerializer.Deserialize<PaymentCompleted>(eventArgs.Body.Span)
                ?? throw new InvalidOperationException("PaymentCompleted payload deserialized to null.");

            using var scope = scopeFactory.CreateScope();
            var ordersService = scope.ServiceProvider.GetRequiredService<IOrdersService>();

            var updated = await ordersService.MarkPaidAsync(paymentCompleted.OrderId, paymentCompleted.PaymentId, stoppingToken);

            if (updated)
            {
                logger.LogInformation(
                    "Marked order {OrderId} as paid (payment {PaymentId}, amount {Amount})",
                    paymentCompleted.OrderId, paymentCompleted.PaymentId, paymentCompleted.Amount);
            }
            else
            {
                // A permanent mismatch (no such order), not a transient failure — retrying via the
                // DLQ wouldn't help, so this is logged and dropped rather than nacked.
                logger.LogWarning(
                    "PaymentCompleted referenced unknown order {OrderId}, dropping", paymentCompleted.OrderId);
            }

            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to process PaymentCompleted message {MessageId}, sending to dead-letter queue",
                eventArgs.BasicProperties.MessageId);
            await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, stoppingToken);
        }
    }
}
