using Mapster;
using PaymentService.API.Models.Requests;
using PaymentService.Application.Models;

namespace PaymentService.API.Mapping;

/// <summary>
/// Every request/response DTO ↔ domain entity mapping lives here, not in controllers. Discovered
/// and applied automatically by <c>services.AddMapster()</c> in Program.cs (Mapster scans the
/// assembly for <see cref="IRegister"/> implementations).
/// </summary>
public class MappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // Словарь статусов провайдера переводится здесь, на границе API: дальше в сервис уезжает
        // уже свой enum. Строки те же, что разбирает клиент провайдера в Infrastructure, — это
        // разные границы (ответ на запрос и тело уведомления), и знать чужие слова им приходится
        // по отдельности. Незнакомый статус считается промежуточным: применять к деньгам то,
        // чего код не понимает, нельзя.
        config.NewConfig<PaymentWebhookRequest, PaymentWebhookNotification>()
            .MapWith(request => new PaymentWebhookNotification
            {
                ProviderPaymentId = request.Payment.Id,
                ClaimedStatus = request.Payment.Status == "succeeded"
                    ? ProviderOperationStatus.Succeeded
                    : request.Payment.Status == "canceled"
                        ? ProviderOperationStatus.Canceled
                        : ProviderOperationStatus.Pending,
                CancellationReason = request.Payment.CancellationDetails == null
                    ? null
                    : request.Payment.CancellationDetails.Reason,
            });
    }
}
