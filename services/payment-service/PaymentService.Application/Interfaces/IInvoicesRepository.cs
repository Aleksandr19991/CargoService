using PaymentService.Domain.Entities;

namespace PaymentService.Application.Interfaces;

/// <summary>
/// Хранилище счетов вместе с их платежами: <c>Payment</c> — часть агрегата <c>Invoice</c>, и
/// отдельного репозитория у него нет.
/// </summary>
public interface IInvoicesRepository
{
    /// <summary>Счёт по заявке вместе с платежами. Null, если счёт ещё не выставлялся.</summary>
    Task<Invoice?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Счёт вместе с платежами по идентификатору транзакции провайдера — так находится платёж,
    /// о котором пришёл webhook.
    /// </summary>
    Task<Invoice?> GetByProviderPaymentIdAsync(string providerPaymentId, CancellationToken cancellationToken);

    /// <summary>Сохраняет счёт вместе с заведёнными платежами одной транзакцией.</summary>
    Task AddAsync(Invoice invoice, CancellationToken cancellationToken);

    /// <summary>Проставляет платежу идентификатор транзакции провайдера и платёжную ссылку.</summary>
    Task SetProviderPaymentAsync(
        Guid paymentId,
        string providerPaymentId,
        string? confirmationUrl,
        CancellationToken cancellationToken);

    /// <summary>
    /// Отмечает платёж успешным и счёт оплаченным — одной транзакцией: платёж без оплаченного
    /// счёта (и наоборот) означал бы расхождение в деньгах.
    /// </summary>
    Task MarkPaymentSucceededAsync(Guid paymentId, CancellationToken cancellationToken);

    Task MarkPaymentFailedAsync(Guid paymentId, string reason, CancellationToken cancellationToken);
}
