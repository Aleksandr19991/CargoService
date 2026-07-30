namespace CargoService.Contracts.Events.V1;

/// <summary>Publisher: cargo-service. Subscribers: orders-service, notification-service, reporting-service.</summary>
public sealed record CargoStatusChanged : IntegrationEvent
{
    public required Guid ShipmentId { get; init; }

    // Added ahead of cargo-service's own implementation (Phase 5) specifically so orders-service
    // can correlate the event back to its own Order row — cargo-service's Shipment entity already
    // carries OrderId (spec.md §2.5), so populating it here is just forwarding a field it already has.
    public required Guid OrderId { get; init; }

    public required string TrackingNumber { get; init; }
    public required string Status { get; init; }
    public string? Location { get; init; }
}
