namespace PaymentService.Application.Models;

/// <summary>Что сервис просит вернуть провайдеру при отмене заявки.</summary>
public sealed record ProviderRefundRequest
{
    /// <summary>
    /// Ключ идемпотентности — та же роль, что и у <see cref="ProviderPaymentRequest.IdempotenceKey"/>,
    /// и здесь она важнее: повтор без ключа вернул бы клиенту деньги дважды.
    /// </summary>
    public required Guid IdempotenceKey { get; init; }

    /// <summary>Идентификатор платежа у провайдера — возврат всегда делается по конкретному платежу.</summary>
    public required string ProviderPaymentId { get; init; }

    /// <summary>Сумма возврата: провайдеры допускают частичный возврат, поэтому она не выводится из платежа.</summary>
    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public string? Description { get; init; }
}
