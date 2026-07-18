using IdentityService.Application.Models;

namespace IdentityService.Application.Interfaces;

/// <summary>Reads and acknowledges outbox rows for the background dispatcher that publishes them to RabbitMQ.</summary>
public interface IOutboxReader
{
    Task<IReadOnlyList<PendingOutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(IReadOnlyCollection<Guid> messageIds, CancellationToken cancellationToken = default);
}
