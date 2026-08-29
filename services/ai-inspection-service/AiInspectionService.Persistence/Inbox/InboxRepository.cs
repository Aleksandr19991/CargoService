using AiInspectionService.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AiInspectionService.Persistence.Inbox;

public class InboxRepository(AppDbContext dbContext) : IInboxRepository
{
    // 23505 = unique_violation. Сверяемся с кодом, а не с текстом сообщения, чтобы не зависеть
    // от локали и версии сервера.
    private const string UniqueViolationSqlState = "23505";

    public Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken) =>
        dbContext.InboxMessages
            .AsNoTracking()
            .AnyAsync(message => message.EventId == eventId, cancellationToken);

    public async Task<bool> TryMarkProcessedAsync(Guid eventId, string eventType, CancellationToken cancellationToken)
    {
        dbContext.InboxMessages.Add(new InboxMessage
        {
            EventId = eventId,
            EventType = eventType,
            ProcessedAtUtc = DateTimeOffset.UtcNow,
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState })
        {
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }
}
