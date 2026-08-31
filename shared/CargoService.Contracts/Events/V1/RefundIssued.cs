namespace CargoService.Contracts.Events.V1;

/// <summary>
/// Publisher: payment-service. Subscribers: orders-service, notification-service.
/// <para>
/// Публикуется, только когда провайдер подтвердил возврат: до этого деньги ещё у платформы, и
/// сообщать клиенту, что они вернулись, нельзя.
/// </para>
/// </summary>
public sealed record RefundIssued : IntegrationEvent
{
    public required Guid OrderId { get; init; }

    /// <summary>Платёж, по которому сделан возврат.</summary>
    public required Guid PaymentId { get; init; }

    public required Guid RefundId { get; init; }

    public required decimal Amount { get; init; }

    /// <summary>Почему деньги вернули — обычно причина отмены заявки.</summary>
    public string? Reason { get; init; }
}
