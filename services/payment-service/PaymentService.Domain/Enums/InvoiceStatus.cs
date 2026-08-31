namespace PaymentService.Domain.Enums;

/// <summary>
/// Состояние счёта. Хранится в БД строкой, поэтому добавление значения миграции не требует.
/// </summary>
public enum InvoiceStatus
{
    /// <summary>Счёт выставлен, оплата ожидается.</summary>
    Issued = 0,

    /// <summary>Оплата получена полностью.</summary>
    Paid = 1,

    /// <summary>Заявка отменена до оплаты — платить больше нечего.</summary>
    Cancelled = 2,

    /// <summary>Заявка отменена после оплаты, деньги возвращены клиенту.</summary>
    Refunded = 3,
}
