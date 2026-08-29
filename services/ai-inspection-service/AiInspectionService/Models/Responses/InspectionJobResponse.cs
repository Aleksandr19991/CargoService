using AiInspectionService.Domain.Enums;

namespace AiInspectionService.API.Models.Responses;

public sealed record InspectionJobResponse
{
    public required Guid Id { get; init; }
    public required Guid ShipmentId { get; init; }
    public required List<Guid> PhotoFileIds { get; init; }
    public required InspectionJobStatus Status { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? FailureReason { get; init; }
    public required List<InspectionResultResponse> Results { get; init; }
}
