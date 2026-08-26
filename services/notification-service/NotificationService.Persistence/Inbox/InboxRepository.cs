using Microsoft.EntityFrameworkCore;
using Npgsql;
using NotificationService.Application.Interfaces;

namespace NotificationService.Persistence.Inbox;

public class InboxRepository(AppDbContext dbContext) : IInboxRepository
{
    // 23505 = unique_violation. Сверяемся с кодом, а не с текстом сообщения, чтобы не зависеть
    // от локали и версии сервера (тот же приём, что в cargo-service).
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
            // Гонка двух одновременных доставок одного события: обе прошли проверку до того, как
            // хоть одна успела записаться. Уникальный ключ отсекает вторую запись — уведомление
            // при этом уже ушло дважды, и здесь это уже не исправить (см. EventConsumer).
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }
}
