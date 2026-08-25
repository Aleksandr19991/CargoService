using CargoService.Domain.Enums;

namespace CargoService.Domain.Entities;

/// <summary>Хронология статусов груза — источник данных для трекинга (append-only).</summary>
public class ShipmentStatusHistory
{
    public Guid Id { get; set; }
    public Guid ShipmentId { get; set; }

    public ShipmentStatus Status { get; set; }
    public DateTimeOffset ChangedAt { get; set; }

    /// <summary>Город или склад, где произошло изменение (ТЗ: Location/Warehouse).</summary>
    public string? Location { get; set; }

    public string? Comment { get; set; }
}
