namespace CargoService.Contracts.Events.V1;

/// <summary>Publisher: logistics-service. Subscribers: cargo-service.</summary>
public sealed record RouteUpdated : IntegrationEvent
{
    public required Guid ShipmentId { get; init; }
    public required Guid RouteId { get; init; }
    public required string CurrentWarehouseCity { get; init; }
}
