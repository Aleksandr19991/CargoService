using AiInspectionService.Application.Interfaces;
using AiInspectionService.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace AiInspectionService.Persistence.Outbox;

public class OutboxReader(AppDbContext context) : IOutboxReader
{
    public async Task<IReadOnlyList<PendingOutboxMessage>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        return await context.OutboxMessages
            .Where(message => message.ProcessedAtUtc == null)
            .OrderBy(message => message.OccurredAtUtc)
            .Take(batchSize)
            .Select(message => new PendingOutboxMessage(message.Id, message.RoutingKey, message.Payload))
            .ToListAsync(cancellationToken);
    }

    public async Task MarkProcessedAsync(
        IReadOnlyCollection<Guid> messageIds,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        await context.OutboxMessages
            .Where(message => messageIds.Contains(message.Id))
            .ExecuteUpdateAsync(setters => setters.SetProperty(message => message.ProcessedAtUtc, now), cancellationToken);
    }
}
