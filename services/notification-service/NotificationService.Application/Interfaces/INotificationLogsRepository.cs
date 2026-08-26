using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.Interfaces;

public interface INotificationLogsRepository
{
    /// <summary>Пишет историю одной обработки события — все попытки отправки разом.</summary>
    Task AddRangeAsync(IReadOnlyList<NotificationLog> logs, CancellationToken cancellationToken);

    /// <summary>
    /// История уведомлений одного получателя, свежие сверху. Возвращает и страницу, и общее
    /// число записей: без второго клиент не нарисует пагинацию.
    /// </summary>
    Task<(IReadOnlyList<NotificationLog> Items, int TotalCount)> GetByRecipientAsync(
        Guid userId,
        NotificationChannel? channel,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
