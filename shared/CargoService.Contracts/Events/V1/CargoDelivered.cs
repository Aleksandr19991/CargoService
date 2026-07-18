namespace CargoService.Contracts.Events.V1;

/// <summary>Publisher: cargo-service. Subscribers: orders-service, notification-service, document-service, reporting-service.</summary>
public sealed record CargoDelivered : IntegrationEvent
{
    public required Guid ShipmentId { get; init; }
    public required Guid OrderId { get; init; }
    public required string TrackingNumber { get; init; }
    public string? ReceivedByName { get; init; }
}
