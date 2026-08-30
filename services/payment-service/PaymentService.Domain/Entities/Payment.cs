using PaymentService.Domain.Enums;

namespace PaymentService.Domain.Entities;

/// <summary>
/// Одна попытка оплаты счёта у платёжного провайдера.
/// <para>
/// Карточных данных здесь нет и быть не должно: клиент вводит их на стороне провайдера, куда его
/// уводит <see cref="ConfirmationUrl"/>, а сервис хранит только идентификатор транзакции —
/// по нему сопоставляются входящие webhook.
/// </para>
/// </summary>
public class Payment
{
    public Guid Id { get; set; }

    public Guid InvoiceId { get; set; }

    /// <summary>
    /// Сумма попытки. Хранится отдельно от суммы счёта, хотя сегодня всегда равна ей: частичная
    /// оплата — обычное требование, и выводить сумму платежа из счёта задним числом нельзя.
    /// </summary>
    public decimal Amount { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>
    /// Идентификатор платежа у провайдера. Null, пока платёж не заведён (или если завести его не
    /// удалось) — до этого момента сопоставлять webhook не с чем.
    /// </summary>
    public string? ProviderPaymentId { get; set; }

    /// <summary>Платёжная ссылка провайдера, по которой клиент вводит карту.</summary>
    public string? ConfirmationUrl { get; set; }

    /// <summary>Почему оплата не состоялась — отказ провайдера или причина отмены платежа.</summary>
    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Момент, когда попытка получила окончательный исход — успех или отказ.</summary>
    public DateTimeOffset? CompletedAt { get; set; }
}
