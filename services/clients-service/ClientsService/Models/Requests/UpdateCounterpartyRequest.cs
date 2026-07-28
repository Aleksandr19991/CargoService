using ClientsService.Domain.Enums;

namespace ClientsService.API.Models.Requests;

public sealed record UpdateCounterpartyRequest
{
    public required CounterpartyType Type { get; init; }
    public string? OrganizationName { get; init; }
    public string? FullName { get; init; }
    public string? Inn { get; init; }
    public required string City { get; init; }
    public required string Phone { get; init; }
    public required string Email { get; init; }
}
