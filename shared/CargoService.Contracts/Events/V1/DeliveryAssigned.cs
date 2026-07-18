namespace CargoService.Contracts.Events.V1;

/// <summary>Publisher: logistics-service. Subscribers: cargo-service.</summary>
public sealed record DeliveryAssigned : IntegrationEvent
{
    public required Guid ShipmentId { get; init; }
    public required Guid OrderId { get; init; }
    public required Guid CourierId { get; init; }
    public required DateTimeOffset ScheduledAtUtc { get; init; }
}
