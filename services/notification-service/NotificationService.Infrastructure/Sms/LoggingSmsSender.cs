using Microsoft.Extensions.Logging;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Models;
using NotificationService.Domain.Enums;

namespace NotificationService.Infrastructure.Sms;

/// <summary>
/// Заглушка канала SMS на время, пока провайдер не подключён: пишет сообщение в лог и
/// отчитывается об успехе. Регистрируется вместо <see cref="TwilioSmsSender"/>, когда в
/// конфигурации нет учётных данных провайдера.
/// <para>
/// Возвращает успех сознательно. Альтернатива — не регистрировать канал вовсе — в локальной
/// разработке и в тестах заполняла бы историю уведомлений строками <c>Failed</c> на каждое
/// событие с SMS-шаблоном, и настоящий сбой провайдера потерялся бы среди них. Ценой этого
/// решения запись «Sent» в истории на dev-стенде не означает доставленного SMS — поэтому
/// каждая такая «отправка» пишется в лог предупреждением, а не информационным сообщением.
/// </para>
/// </summary>
public class LoggingSmsSender(ILogger<LoggingSmsSender> logger) : INotificationSender
{
    public NotificationChannel Channel => NotificationChannel.Sms;

    public Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "SMS provider is not configured; message to {Recipient} was not sent: {Body}",
            message.RecipientContact,
            message.Body);

        return Task.FromResult(NotificationSendResult.Success());
    }
}
