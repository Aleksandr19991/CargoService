using CargoService.Domain.Enums;

namespace CargoService.Application.Models;

/// <summary>Одно изменение статуса груза: новый статус плюс где и с каким комментарием он зафиксирован.</summary>
public sealed record ShipmentStatusChange
{
    public required ShipmentStatus Status { get; init; }

    /// <summary>Город/склад, где зафиксирован статус.</summary>
    public string? Location { get; init; }

    public string? Comment { get; init; }
}
