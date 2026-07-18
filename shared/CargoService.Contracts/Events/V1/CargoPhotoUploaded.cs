namespace CargoService.Contracts.Events.V1;

/// <summary>Publisher: cargo-service. Subscribers: ai-inspection-service.</summary>
public sealed record CargoPhotoUploaded : IntegrationEvent
{
    public required Guid ShipmentId { get; init; }
    public required string TrackingNumber { get; init; }
    public required IReadOnlyCollection<Guid> PhotoFileIds { get; init; }
}
