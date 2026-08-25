using CargoService.Domain.Enums;

namespace CargoService.API.Models.Responses;

public sealed record ShipmentStatusHistoryResponse
{
    public required Guid Id { get; init; }
    public required ShipmentStatus Status { get; init; }
    public required DateTimeOffset ChangedAt { get; init; }
    public string? Location { get; init; }
    public string? Comment { get; init; }
}
