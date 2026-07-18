namespace CargoService.Contracts.Events.V1;

/// <summary>Publisher: orders-service. Subscribers: cargo-service, payment-service, notification-service.</summary>
public sealed record OrderConfirmed : IntegrationEvent
{
    public required Guid OrderId { get; init; }
    public required string OrderNumber { get; init; }
    public required decimal CalculatedPrice { get; init; }
}
