namespace CargoService.Contracts.Events.V1;

/// <summary>Publisher: ai-inspection-service. Subscribers: cargo-service, notification-service.</summary>
public sealed record PackageIntegrityAssessed : IntegrationEvent
{
    public required Guid ShipmentId { get; init; }
    public required Guid InspectionJobId { get; init; }
    public required bool DamageDetected { get; init; }
    public required double Confidence { get; init; }
}
