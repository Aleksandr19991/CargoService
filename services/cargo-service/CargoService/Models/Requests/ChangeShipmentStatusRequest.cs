using CargoService.Domain.Enums;

namespace CargoService.API.Models.Requests;

public sealed record ChangeShipmentStatusRequest
{
    public required ShipmentStatus Status { get; init; }

    /// <summary>Город/склад, где зафиксирован статус — попадёт в историю.</summary>
    public string? Location { get; init; }

    public string? Comment { get; init; }
}
