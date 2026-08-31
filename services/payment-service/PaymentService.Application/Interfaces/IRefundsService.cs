using PaymentService.Domain.Entities;

namespace PaymentService.Application.Interfaces;

public interface IRefundsService
{
    /// <summary>
    /// Закрывает денежную сторону отменённой заявки: неоплаченный счёт отменяется, оплаченный —
    /// возвращается клиенту.
    /// </summary>
    Task RefundForCancelledOrderAsync(
        Guid orderId,
        string orderNumber,
        string? reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Возвращает деньги по конкретному платежу. Отдельно от метода выше, потому что оплата
    /// может прийти уже после отмены заявки — тогда возвращать надо тот платёж, который только
    /// что подтвердил провайдер (см. <c>PaymentWebhooksService</c>).
    /// </summary>
    Task RefundPaymentAsync(Invoice invoice, Payment payment, string reason, CancellationToken cancellationToken);
}
