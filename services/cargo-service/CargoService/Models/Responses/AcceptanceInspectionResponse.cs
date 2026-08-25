using CargoService.Domain.Enums;

namespace CargoService.API.Models.Responses;

public sealed record AcceptanceInspectionResponse
{
    public required Guid Id { get; init; }
    public required Guid InspectedByUserId { get; init; }
    public required PackagingCondition PackagingCondition { get; init; }
    public required CargoCondition CargoCondition { get; init; }
    public string? Comment { get; init; }
    public required List<Guid> PhotoFileIds { get; init; }
    public required DateTimeOffset InspectedAt { get; init; }
}
