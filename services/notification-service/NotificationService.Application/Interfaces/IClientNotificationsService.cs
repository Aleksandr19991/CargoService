using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.Interfaces;

/// <summary>
/// Личный кабинет клиента: своя история уведомлений и свои настройки каналов. Идентификатор
/// пользователя всегда приходит из токена — чужую историю через этот сервис не получить.
/// </summary>
public interface IClientNotificationsService
{
    Task<(IReadOnlyList<NotificationLog> Items, int TotalCount)> GetHistoryAsync(
        Guid userId,
        NotificationChannel? channel,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>Настройки клиента; если он их не менял — значения по умолчанию (всё включено).</summary>
    Task<NotificationPreference> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken);

    Task<NotificationPreference> UpdatePreferencesAsync(
        NotificationPreference preference,
        CancellationToken cancellationToken);
}
