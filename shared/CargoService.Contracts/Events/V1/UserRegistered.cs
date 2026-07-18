namespace CargoService.Contracts.Events.V1;

/// <summary>Publisher: identity-service. Subscribers: clients-service.</summary>
public sealed record UserRegistered : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public required string Name { get; init; }
    public required string LastName { get; init; }
    public required string Phone { get; init; }
    public required string Email { get; init; }
}
