using System.Text;
using CargoService.Contracts.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Interfaces;
using PaymentService.Infrastructure.Messaging;
using RabbitMQ.Client;

namespace PaymentService.Infrastructure.Outbox;

/// <summary>
/// Polls <see cref="IOutboxReader"/> for unpublished outbox rows and publishes them to
/// <see cref="RabbitMqConventions.EventsExchange"/>. Runs for the lifetime of the app; any
/// failure (RabbitMQ unreachable, connection dropped) is caught and retried after a delay
/// rather than crashing the host, since a hosted service throwing unhandled by default takes
/// the whole app down with it.
/// </summary>
public class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    RabbitMqOptions options,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
    private const int BatchSize = 20;

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
                logger.LogError(ex, "Outbox dispatcher failed, reconnecting in {Delay}", ReconnectDelay);
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

        while (!stoppingToken.IsCancellationRequested)
        {
            await DispatchPendingAsync(channel, stoppingToken);
            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task DispatchPendingAsync(IChannel channel, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outboxReader = scope.ServiceProvider.GetRequiredService<IOutboxReader>();

        var pending = await outboxReader.GetPendingAsync(BatchSize, cancellationToken);
        if (pending.Count == 0)
            return;

        var publishedIds = new List<Guid>(pending.Count);
        foreach (var message in pending)
        {
            try
            {
                var body = Encoding.UTF8.GetBytes(message.Payload);
                var properties = new BasicProperties
                {
                    Persistent = true,
                    MessageId = message.Id.ToString(),
                    ContentType = "application/json",
                };

                await channel.BasicPublishAsync(
                    RabbitMqConventions.EventsExchange,
                    message.RoutingKey,
                    mandatory: false,
                    properties,
                    body,
                    cancellationToken);

                publishedIds.Add(message.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish outbox message {MessageId} ({RoutingKey})", message.Id, message.RoutingKey);
            }
        }

        if (publishedIds.Count > 0)
            await outboxReader.MarkProcessedAsync(publishedIds, cancellationToken);
    }
}
