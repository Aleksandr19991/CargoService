namespace CargoService.Contracts.Events.V1;

/// <summary>Publisher: pricing-service. Subscribers: orders-service (invalidates its tariff cache).</summary>
public sealed record TariffChanged : IntegrationEvent
{
    public required Guid TariffId { get; init; }
    public required string Category { get; init; }
    public required string Code { get; init; }
    public required decimal Price { get; init; }
}
