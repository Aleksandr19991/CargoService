using CargoService.Application.Models;

namespace CargoService.Application.Interfaces;

public interface IOutboxReader
{
    Task<IReadOnlyList<PendingOutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default);
    Task MarkProcessedAsync(IReadOnlyCollection<Guid> messageIds, CancellationToken cancellationToken = default);
}
