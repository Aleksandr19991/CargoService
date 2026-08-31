using PaymentService.Domain.Enums;

namespace PaymentService.Domain.Entities;

/// <summary>
/// Счёт по подтверждённой заявке — сколько сервис ждёт от клиента и получил ли.
/// <para>
/// Счёт и попытки оплаты разделены сознательно: клиент может не довести оплату до конца, уйти со
/// страницы провайдера, получить отказ банка и попробовать снова — это разные попытки одной и той
/// же суммы, и складывать их в одну строку значило бы терять историю. <see cref="Payments"/> —
/// часть этого агрегата: платежи заводятся, читаются и меняются только через счёт.
/// </para>
/// </summary>
public class Invoice
{
    public Guid Id { get; set; }

    // References Order.Id / Order.Number in orders-service — без FK и навигации, «database per service».
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    /// <summary>Валюта расчётов (ISO 4217) на момент выставления счёта.</summary>
    public string Currency { get; set; } = string.Empty;

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Issued;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    public ICollection<Payment> Payments { get; set; } = [];

    public ICollection<Refund> Refunds { get; set; } = [];
}
