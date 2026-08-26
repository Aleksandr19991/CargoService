using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Application;

public class ClientNotificationsService(
    INotificationLogsRepository logsRepository,
    IRecipientsRepository recipientsRepository) : IClientNotificationsService
{
    public Task<(IReadOnlyList<NotificationLog> Items, int TotalCount)> GetHistoryAsync(
        Guid userId,
        NotificationChannel? channel,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        logsRepository.GetByRecipientAsync(userId, channel, page, pageSize, cancellationToken);

    public async Task<NotificationPreference> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken) =>
        await recipientsRepository.GetPreferenceAsync(userId, cancellationToken)
        // Клиент настроек не касался — отдаём умолчание, а не 404: с точки зрения кабинета
        // настройки есть всегда, вопрос лишь в том, сохранял ли их кто-то (см. NotificationPreference).
        ?? new NotificationPreference { UserId = userId };

    public async Task<NotificationPreference> UpdatePreferencesAsync(
        NotificationPreference preference,
        CancellationToken cancellationToken)
    {
        await recipientsRepository.UpsertPreferenceAsync(preference, cancellationToken);
        return preference;
    }
}
