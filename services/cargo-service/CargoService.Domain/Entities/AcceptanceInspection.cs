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

    // Результат автоматической проверки фото из ai-inspection-service (событие
    // PackageIntegrityAssessed, spec.md §2.5). Всё nullable: до ответа ИИ — а его может не быть
    // вовсе, если фото не приложили — полей просто нет.
    public Guid? AiInspectionJobId { get; set; }
    public bool? AiDamageDetected { get; set; }
    public double? AiConfidence { get; set; }
    public DateTimeOffset? AiAssessedAt { get; set; }

    /// <summary>
    /// Оценка ИИ разошлась с оценкой сотрудника — повод перепроверить груз. Ставится только при
    /// достаточной уверенности модели (см. <c>ShipmentsService</c>), поэтому <c>false</c> здесь
    /// значит «расхождения нет либо ИИ не уверен», а не «ИИ подтвердил».
    /// </summary>
    public bool? HasAssessmentDiscrepancy { get; set; }
}
