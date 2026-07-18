namespace CargoService.Contracts.Events.V1;

/// <summary>Publisher: orders-service. Subscribers: payment-service, notification-service.</summary>
public sealed record OrderCancelled : IntegrationEvent
{
    public required Guid OrderId { get; init; }
    public required string OrderNumber { get; init; }
    public string? Reason { get; init; }
}
