using PaymentService.Application.Models;

namespace PaymentService.Application.Interfaces;

public interface IPaymentWebhooksService
{
    /// <summary>Применяет уведомление провайдера к платежу и публикует исход заинтересованным сервисам.</summary>
    Task<WebhookOutcome> HandleAsync(PaymentWebhookNotification notification, CancellationToken cancellationToken);
}
