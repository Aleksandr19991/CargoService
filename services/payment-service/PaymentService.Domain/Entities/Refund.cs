using PaymentService.Domain.Enums;

namespace PaymentService.Domain.Entities;

/// <summary>
/// Возврат денег по ранее проведённому платежу.
/// <para>
/// Отдельная сущность, а не статус платежа: возврат — самостоятельная транзакция у провайдера
/// со своим идентификатором, своим исходом и своим моментом времени, и записать её в строку
/// платежа значило бы затереть обстоятельства оплаты обстоятельствами возврата. По той же
/// причине сумма своя: провайдеры допускают частичный возврат.
/// </para>
/// </summary>
public class Refund
{
    public Guid Id { get; set; }

    public Guid InvoiceId { get; set; }

    /// <summary>Платёж, по которому возвращаются деньги.</summary>
    public Guid PaymentId { get; set; }

    public decimal Amount { get; set; }

    public RefundStatus Status { get; set; } = RefundStatus.Pending;

    /// <summary>Идентификатор возврата у провайдера. Null, пока возврат не заведён.</summary>
    public string? ProviderRefundId { get; set; }

    /// <summary>Почему деньги возвращаются — причина отмены заявки.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Почему возврат не состоялся.</summary>
    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
