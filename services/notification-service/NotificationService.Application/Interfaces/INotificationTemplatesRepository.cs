using NotificationService.Domain.Entities;

namespace NotificationService.Application.Interfaces;

public interface INotificationTemplatesRepository
{
    /// <summary>Все шаблоны повода — по одному на канал, в котором уведомление предусмотрено.</summary>
    Task<IReadOnlyList<NotificationTemplate>> GetByCodeAsync(string code, CancellationToken cancellationToken);
}
