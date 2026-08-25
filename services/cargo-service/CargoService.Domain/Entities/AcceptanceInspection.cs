using CargoService.Domain.Enums;

namespace CargoService.Domain.Entities;

/// <summary>Акт приёмки груза сотрудником: зафиксированное состояние упаковки и груза плюс фото.</summary>
public class AcceptanceInspection
{
    public Guid Id { get; set; }
    public Guid ShipmentId { get; set; }

    // References User.Id in identity-service (сотрудник склада, проводивший приёмку) — no
    // FK/navigation across services.
    public Guid InspectedByUserId { get; set; }

    public PackagingCondition PackagingCondition { get; set; }
    public CargoCondition CargoCondition { get; set; }

    public string? Comment { get; set; }

    // Идентификаторы файлов в File Storage (spec.md Фаза 10) — сам файл здесь не хранится.
    // Ложится в Postgres-колонку uuid[], отдельной таблицы не заводим: список коротким и
    // читается всегда целиком вместе с актом.
    public List<Guid> PhotoFileIds { get; set; } = [];

    public DateTimeOffset InspectedAt { get; set; }
}
