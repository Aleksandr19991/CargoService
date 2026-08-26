using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Text;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Models;
using NotificationService.Domain.Enums;

namespace NotificationService.Infrastructure.Email;

/// <summary>
/// Отправка писем по SMTP (MailKit). Годится и для локального сборщика почты, и для
/// провайдера — см. <see cref="SmtpOptions"/>.
/// </summary>
public class SmtpEmailSender(SmtpOptions options, ILogger<SmtpEmailSender> logger) : INotificationSender
{
    public NotificationChannel Channel => NotificationChannel.Email;

    public async Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        var mail = new MimeMessage();
        mail.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        mail.To.Add(MailboxAddress.Parse(message.RecipientContact));
        mail.Subject = message.Subject ?? string.Empty;
        mail.Body = new TextPart(TextFormat.Plain) { Text = message.Body };

        // Соединение на письмо, а не общий переиспользуемый клиент: SMTP-сессия держит сокет и
        // не потокобезопасна, а поток писем здесь — единицы в минуту, так что накладные расходы
        // на установку соединения несопоставимы со сложностью пула с проверкой живости сессии.
        using var client = new SmtpClient();

        try
        {
            var securityOptions = options.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            await client.ConnectAsync(options.Host, options.Port, securityOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(options.UserName))
                await client.AuthenticateAsync(options.UserName, options.Password ?? string.Empty, cancellationToken);

            await client.SendAsync(mail, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);

            logger.LogInformation("Email sent to {Recipient}", message.RecipientContact);
            return NotificationSendResult.Success();
        }
        catch (OperationCanceledException)
        {
            // Остановка сервиса — не отказ провайдера: наверх уходит отмена, а не строка Failed
            // в истории, иначе рестарт помечал бы неотправленные письма как окончательно упавшие.
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to send email to {Recipient}", message.RecipientContact);
            return NotificationSendResult.Failure($"SMTP: {exception.Message}");
        }
    }
}
