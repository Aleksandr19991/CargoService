using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Persistence.Repositories;

public class NotificationLogsRepository(AppDbContext dbContext) : INotificationLogsRepository
{
    public async Task AddRangeAsync(IReadOnlyList<NotificationLog> logs, CancellationToken cancellationToken)
    {
        await dbContext.NotificationLogs.AddRangeAsync(logs, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<NotificationLog> Items, int TotalCount)> GetByRecipientAsync(
        Guid userId,
        NotificationChannel? channel,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.NotificationLogs
            .AsNoTracking()
            .Where(log => log.RecipientUserId == userId);

        if (channel is not null)
            query = query.Where(log => log.Channel == channel);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(log => log.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
