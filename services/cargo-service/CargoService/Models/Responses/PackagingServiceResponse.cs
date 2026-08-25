using CargoService.Domain.Enums;

namespace CargoService.API.Models.Responses;

public sealed record PackagingServiceResponse
{
    public required Guid Id { get; init; }
    public required PackagingType Type { get; init; }
    public required Guid PerformedByUserId { get; init; }
    public required DateTimeOffset PerformedAt { get; init; }
}
