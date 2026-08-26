using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;

namespace NotificationService.Persistence.Repositories;

public class NotificationLogsRepository(AppDbContext dbContext) : INotificationLogsRepository
{
    public async Task AddRangeAsync(IReadOnlyList<NotificationLog> logs, CancellationToken cancellationToken)
    {
        await dbContext.NotificationLogs.AddRangeAsync(logs, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
