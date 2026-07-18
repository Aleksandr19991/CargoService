namespace CargoService.Contracts.Events.V1;

/// <summary>Publisher: cargo-service. Subscribers: notification-service, document-service, logistics-service.</summary>
public sealed record CargoAccepted : IntegrationEvent
{
    public required Guid ShipmentId { get; init; }
    public required Guid OrderId { get; init; }
    public required string TrackingNumber { get; init; }
    public required string PackagingCondition { get; init; }
    public required string CargoCondition { get; init; }
    public required Guid InspectedByUserId { get; init; }
}
