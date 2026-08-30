namespace PaymentService.Application.Models;

/// <summary>Исход обращения к провайдеру по возврату — см. <see cref="ProviderPaymentResult"/>.</summary>
public sealed record ProviderRefundResult
{
    public required bool IsSuccess { get; init; }

    public string? FailureReason { get; init; }

    /// <summary>Идентификатор возврата у провайдера. Null при отказе.</summary>
    public string? ProviderRefundId { get; init; }

    public ProviderOperationStatus Status { get; init; }

    public decimal Amount { get; init; }

    public static ProviderRefundResult Success(string providerRefundId, ProviderOperationStatus status, decimal amount) =>
        new()
        {
            IsSuccess = true,
            ProviderRefundId = providerRefundId,
            Status = status,
            Amount = amount,
        };

    public static ProviderRefundResult Failure(string reason) =>
        new() { IsSuccess = false, FailureReason = reason };
}
