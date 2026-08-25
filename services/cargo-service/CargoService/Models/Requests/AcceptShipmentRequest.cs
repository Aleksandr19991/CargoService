using CargoService.Domain.Enums;

namespace CargoService.API.Models.Requests;

public sealed record AcceptShipmentRequest
{
    public required PackagingCondition PackagingCondition { get; init; }
    public required CargoCondition CargoCondition { get; init; }

    public string? Comment { get; init; }

    // Идентификаторы уже загруженных в File Storage файлов (Фаза 10) — байты через cargo-service
    // не проходят. Пусто, если фото приложат позже через POST /shipments/{id}/photos.
    public List<Guid> PhotoFileIds { get; init; } = [];

    /// <summary>Фактически выполненные при приёмке услуги упаковки; пусто, если упаковка не требовалась.</summary>
    public List<PackagingType> PerformedPackagingTypes { get; init; } = [];

    /// <summary>Склад/город приёмки — попадёт в историю статусов.</summary>
    public string? Location { get; init; }
}
