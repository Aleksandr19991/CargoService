namespace NotificationService.Application.Models;

/// <summary>
/// Исход одной попытки отправки. Отказ провайдера возвращается значением, а не исключением:
/// «письмо не ушло» — обычный рабочий исход, который должен лечь в историю строкой со статусом
/// <c>Failed</c> и причиной, а не свалить обработку события целиком.
/// </summary>
public sealed record NotificationSendResult
{
    public required bool IsSuccess { get; init; }

    /// <summary>Причина отказа для колонки FailureReason. Null при успехе.</summary>
    public string? FailureReason { get; init; }

    public static NotificationSendResult Success() => new() { IsSuccess = true };

    public static NotificationSendResult Failure(string reason) =>
        new() { IsSuccess = false, FailureReason = reason };
}
