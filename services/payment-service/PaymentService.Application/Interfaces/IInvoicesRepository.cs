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

    /// <summary>Сохраняет счёт вместе с заведёнными платежами одной транзакцией.</summary>
    Task AddAsync(Invoice invoice, CancellationToken cancellationToken);

    /// <summary>Проставляет платежу идентификатор транзакции провайдера и платёжную ссылку.</summary>
    Task SetProviderPaymentAsync(
        Guid paymentId,
        string providerPaymentId,
        string? confirmationUrl,
        CancellationToken cancellationToken);

    Task MarkPaymentFailedAsync(Guid paymentId, string reason, CancellationToken cancellationToken);
}
