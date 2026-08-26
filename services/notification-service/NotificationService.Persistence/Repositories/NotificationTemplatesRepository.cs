using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;

namespace NotificationService.Persistence.Repositories;

public class NotificationTemplatesRepository(AppDbContext dbContext) : INotificationTemplatesRepository
{
    public async Task<IReadOnlyList<NotificationTemplate>> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken) =>
        await dbContext.NotificationTemplates
            .AsNoTracking()
            .Where(template => template.Code == code)
            .OrderBy(template => template.Channel)
            .ToListAsync(cancellationToken);
}
