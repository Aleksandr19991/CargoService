namespace CargoService.Application.Models;

public enum ShipmentOperationResult
{
    NotFound,

    /// <summary>Операция несовместима с текущим состоянием груза (например, приёмка уже принятого).</summary>
    Conflict,

    Success
}
