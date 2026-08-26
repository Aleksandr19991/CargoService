using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Models;
using NotificationService.Domain.Enums;

namespace NotificationService.Infrastructure.Sms;

/// <summary>
/// Отправка SMS через REST API провайдера (контракт Twilio: form-POST на
/// <c>/2010-04-01/Accounts/{sid}/Messages.json</c> с Basic-аутентификацией).
/// </summary>
public class TwilioSmsSender(
    HttpClient httpClient,
    TwilioSmsOptions options,
    ILogger<TwilioSmsSender> logger) : INotificationSender
{
    private const int MaxFailureReasonLength = 500;

    public NotificationChannel Channel => NotificationChannel.Sms;

    public async Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        // Subject сообщения намеренно игнорируется: у SMS темы нет, а склеивать её с телом
        // значило бы удлинять платное сообщение текстом, которого в шаблоне канала и не было.
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = message.RecipientContact,
            ["From"] = options.FromNumber ?? string.Empty,
            ["Body"] = message.Body,
        });

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{options.AccountSid}:{options.AuthToken}"));

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"2010-04-01/Accounts/{options.AccountSid}/Messages.json")
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("SMS sent to {Recipient}", message.RecipientContact);
                return NotificationSendResult.Success();
            }

            // Отказ провайдера (неверный номер, кончился баланс, отклонённый текст) — рабочий
            // исход, а не сбой: тело ответа несёт причину и должно попасть в историю, но
            // целиком оно не нужно и в колонку FailureReason не влезет.
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var reason = $"SMS provider {(int)response.StatusCode}: {Truncate(body)}";

            logger.LogError("Failed to send SMS to {Recipient}: {Reason}", message.RecipientContact, reason);
            return NotificationSendResult.Failure(reason);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Остановка сервиса, а не отказ провайдера — см. тот же случай в SmtpEmailSender.
            throw;
        }
        catch (Exception exception)
        {
            // Сюда попадают и обрывы связи, и исчерпанные Polly-политики (retry/circuit breaker
            // навешаны на клиента при регистрации).
            logger.LogError(exception, "Failed to send SMS to {Recipient}", message.RecipientContact);
            return NotificationSendResult.Failure($"SMS provider: {exception.Message}");
        }
    }

    private static string Truncate(string value) =>
        value.Length <= MaxFailureReasonLength ? value : value[..MaxFailureReasonLength];
}
