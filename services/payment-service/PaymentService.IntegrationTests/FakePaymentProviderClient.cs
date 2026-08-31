using PaymentService.Application.Interfaces;
using PaymentService.Application.Models;

namespace PaymentService.IntegrationTests;

/// <summary>
/// Управляемая заглушка провайдера: тест задаёт, что он ответит на перепроверку статуса и на
/// возврат. Настоящий эквайринг разворачивать ради проверки эндпоинта незачем.
/// </summary>
public class FakePaymentProviderClient : IPaymentProviderClient
{
    /// <summary>Что провайдер сообщает о платеже при перепроверке.</summary>
    public ProviderOperationStatus PaymentStatus { get; set; } = ProviderOperationStatus.Succeeded;

    /// <summary>Провайдер недоступен — перепроверить статус нечем.</summary>
    public bool IsUnavailable { get; set; }

    public string? CancellationReason { get; set; }

    public List<ProviderRefundRequest> Refunds { get; } = [];

    public Task<ProviderPaymentResult> CreatePaymentAsync(
        ProviderPaymentRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(ProviderPaymentResult.Success(
            $"provider-{Guid.NewGuid():N}",
            ProviderOperationStatus.Pending,
            request.Amount,
            "https://provider.test/pay"));

    public Task<ProviderPaymentResult> GetPaymentAsync(string providerPaymentId, CancellationToken cancellationToken) =>
        Task.FromResult(IsUnavailable
            ? ProviderPaymentResult.Failure("provider is unavailable")
            : ProviderPaymentResult.Success(providerPaymentId, PaymentStatus, 1250.50m, cancellationReason: CancellationReason));

    public Task<ProviderRefundResult> RefundAsync(ProviderRefundRequest request, CancellationToken cancellationToken)
    {
        Refunds.Add(request);

        return Task.FromResult(ProviderRefundResult.Success(
            $"refund-{Guid.NewGuid():N}",
            ProviderOperationStatus.Succeeded,
            request.Amount));
    }
}
