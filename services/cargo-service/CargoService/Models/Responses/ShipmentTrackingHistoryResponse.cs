using CargoService.Domain.Enums;

namespace CargoService.API.Models.Responses;

/// <summary>
/// Запись трекинга для анонимного запроса. В отличие от
/// <see cref="ShipmentStatusHistoryResponse"/> здесь нет ни <c>Id</c>, ни <c>Comment</c>:
/// комментарии пишет склад для себя и в них попадают внутренние пометки.
/// </summary>
public sealed record ShipmentTrackingHistoryResponse
{
    public required ShipmentStatus Status { get; init; }
    public required DateTimeOffset ChangedAt { get; init; }
    public string? Location { get; init; }
}
