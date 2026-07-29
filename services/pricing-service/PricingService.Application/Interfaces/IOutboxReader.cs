using PricingService.Application.Models;

namespace PricingService.Application.Interfaces;

public interface IOutboxReader
{
    Task<IReadOnlyList<PendingOutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default);
    Task MarkProcessedAsync(IReadOnlyCollection<Guid> messageIds, CancellationToken cancellationToken = default);
}
