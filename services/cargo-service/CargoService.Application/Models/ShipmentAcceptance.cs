using CargoService.Domain.Enums;

namespace CargoService.Application.Models;

/// <summary>Данные приёмки: что зафиксировал сотрудник, какие услуги упаковки выполнил и где.</summary>
public sealed record ShipmentAcceptance
{
    /// <summary>Сотрудник склада, проводящий приёмку (claim `sub` его токена).</summary>
    public required Guid InspectedByUserId { get; init; }

    public required PackagingCondition PackagingCondition { get; init; }
    public required CargoCondition CargoCondition { get; init; }
    public string? Comment { get; init; }

    // Файлы уже загружены в File Storage (Фаза 10) — сюда приходят только их идентификаторы,
    // байты через cargo-service не проходят.
    public required IReadOnlyCollection<Guid> PhotoFileIds { get; init; }

    /// <summary>Фактически выполненные услуги упаковки — по одной записи PackagingService на вид.</summary>
    public required IReadOnlyCollection<PackagingType> PerformedPackagingTypes { get; init; }

    /// <summary>Склад/город приёмки — попадает в запись истории статусов.</summary>
    public string? Location { get; init; }
}
