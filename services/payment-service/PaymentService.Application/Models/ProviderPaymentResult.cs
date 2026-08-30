namespace PaymentService.Application.Models;

/// <summary>
/// Исход обращения к провайдеру по платежу. Отказ возвращается значением, а не исключением, по
/// той же причине, что и в notification-service: «провайдер не принял платёж» — рабочий исход,
/// который должен лечь в запись платежа причиной и уехать событием <c>PaymentFailed</c>, а не
/// свалить обработку события целиком.
/// <para>
/// Плоская запись, без вложенного объекта платежа: при отказе заполнять было бы нечего, и
/// вызывающему пришлось бы разбирать два уровня null вместо одного флага.
/// </para>
/// </summary>
public sealed record ProviderPaymentResult
{
    public required bool IsSuccess { get; init; }

    /// <summary>Причина отказа провайдера. Null при успехе.</summary>
    public string? FailureReason { get; init; }

    /// <summary>Идентификатор платежа у провайдера — по нему сопоставляются webhook. Null при отказе.</summary>
    public string? ProviderPaymentId { get; init; }

    public ProviderOperationStatus Status { get; init; }

    /// <summary>
    /// Платёжная ссылка, по которой клиент вводит карту. Есть только пока платёж ждёт оплаты:
    /// у завершённого или отменённого её уже нет.
    /// </summary>
    public string? ConfirmationUrl { get; init; }

    public decimal Amount { get; init; }

    /// <summary>
    /// Почему провайдер отменил платёж (недостаток средств, отказ банка, истёкшее время).
    /// Заполнено только у <see cref="ProviderOperationStatus.Canceled"/> — это не отказ вызова,
    /// а нормально полученный ответ, поэтому лежит отдельно от <see cref="FailureReason"/>.
    /// </summary>
    public string? CancellationReason { get; init; }

    public static ProviderPaymentResult Success(
        string providerPaymentId,
        ProviderOperationStatus status,
        decimal amount,
        string? confirmationUrl = null,
        string? cancellationReason = null) =>
        new()
        {
            IsSuccess = true,
            ProviderPaymentId = providerPaymentId,
            Status = status,
            Amount = amount,
            ConfirmationUrl = confirmationUrl,
            CancellationReason = cancellationReason,
        };

    public static ProviderPaymentResult Failure(string reason) =>
        new() { IsSuccess = false, FailureReason = reason };
}
