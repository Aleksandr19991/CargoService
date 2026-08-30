using System.Text.Json.Serialization;

namespace PaymentService.API.Models.Requests;

/// <summary>
/// Уведомление платёжного провайдера (контракт ЮKassa): <c>{ "event": "payment.succeeded",
/// "object": { ... } }</c>. Единственное место в сервисе, где живёт форма чужого JSON — дальше
/// в Application уезжает уже разобранное <c>PaymentWebhookNotification</c>.
/// </summary>
public sealed record PaymentWebhookRequest
{
    /// <summary>Что произошло: <c>payment.succeeded</c>, <c>payment.canceled</c> и подобное.</summary>
    [JsonPropertyName("event")]
    public required string Event { get; init; }

    /// <summary>
    /// Сам платёж. В JSON провайдера поле называется <c>object</c> — здесь оно переименовано,
    /// потому что свойство с таким именем читалось бы как тип, а не как платёж.
    /// </summary>
    [JsonPropertyName("object")]
    public required PaymentWebhookPayment Payment { get; init; }
}

public sealed record PaymentWebhookPayment
{
    /// <summary>Идентификатор платежа у провайдера — по нему находится наша запись.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("cancellation_details")]
    public PaymentWebhookCancellationDetails? CancellationDetails { get; init; }
}

public sealed record PaymentWebhookCancellationDetails
{
    [JsonPropertyName("party")]
    public string? Party { get; init; }

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}
