namespace CargoService.Contracts.Events.V1;

/// <summary>Publisher: orders-service. Subscribers: notification-service, reporting-service.</summary>
public sealed record OrderCreated : IntegrationEvent
{
    public required Guid OrderId { get; init; }
    public required string OrderNumber { get; init; }
    public required Guid ClientAccountId { get; init; }
    public required string OriginCity { get; init; }
    public required string DestinationCity { get; init; }
}
