namespace CargoService.Contracts.Events.V1;

/// <summary>Publisher: cargo-service. Subscribers: orders-service, notification-service, reporting-service.</summary>
public sealed record CargoStatusChanged : IntegrationEvent
{
    public required Guid ShipmentId { get; init; }
    public required string TrackingNumber { get; init; }
    public required string Status { get; init; }
    public string? Location { get; init; }
}
