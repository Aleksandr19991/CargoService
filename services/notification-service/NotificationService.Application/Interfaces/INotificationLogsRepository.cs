using NotificationService.Domain.Entities;

namespace NotificationService.Application.Interfaces;

public interface INotificationLogsRepository
{
    /// <summary>Пишет историю одной обработки события — все попытки отправки разом.</summary>
    Task AddRangeAsync(IReadOnlyList<NotificationLog> logs, CancellationToken cancellationToken);
}
