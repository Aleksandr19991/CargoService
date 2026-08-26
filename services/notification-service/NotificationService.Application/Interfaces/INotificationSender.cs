using NotificationService.Application.Models;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.Interfaces;

/// <summary>
/// Отправитель одного канала. Реализации живут в Infrastructure (SMTP, SMS-провайдер) и
/// регистрируются все разом — нужный выбирается по <see cref="Channel"/> через
/// <see cref="INotificationSenderRegistry"/>.
/// </summary>
public interface INotificationSender
{
    NotificationChannel Channel { get; }

    Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken);
}
