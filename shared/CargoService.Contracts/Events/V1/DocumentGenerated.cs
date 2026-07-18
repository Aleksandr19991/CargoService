namespace CargoService.Contracts.Events.V1;

/// <summary>Publisher: document-service. Subscribers: notification-service, orders-service.</summary>
public sealed record DocumentGenerated : IntegrationEvent
{
    public required Guid ShipmentId { get; init; }
    public required Guid OrderId { get; init; }
    public required string DocumentType { get; init; }
    public required Guid DocumentFileId { get; init; }
}
