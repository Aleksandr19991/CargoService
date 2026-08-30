namespace PaymentService.Application.Models;

/// <summary>
/// Уведомление провайдера об исходе платежа, приведённое к словарю сервиса. Вокабуляр провайдера
/// (<c>payment.succeeded</c>, <c>canceled</c> и прочее) кончается на границе API — сюда приходит
/// уже разобранное.
/// <para>
/// Статус назван «заявленным», а не «текущим», намеренно: тело webhook — это то, что прислал
/// неаутентифицированный запрос снаружи, а не установленный факт. Что с ним делать, решает
/// <c>PaymentWebhooksService</c>.
/// </para>
/// </summary>
public sealed record PaymentWebhookNotification
{
    public required string ProviderPaymentId { get; init; }

    public required ProviderOperationStatus ClaimedStatus { get; init; }

    public string? CancellationReason { get; init; }
}
