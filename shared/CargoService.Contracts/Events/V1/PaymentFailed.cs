namespace CargoService.Contracts.Events.V1;

/// <summary>Publisher: payment-service. Subscribers: orders-service, notification-service.</summary>
public sealed record PaymentFailed : IntegrationEvent
{
    public required Guid OrderId { get; init; }
    public required Guid PaymentId { get; init; }
    public required string Reason { get; init; }
}
