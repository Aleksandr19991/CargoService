namespace DocumentService.Application.Models;

/// <summary>
/// Данные акта осмотра при повреждении — документа, на который клиент ссылается в претензии.
/// Поэтому здесь важны не только состояния, но и то, кем и когда повреждение зафиксировано, и
/// что о нём сказала автоматическая проверка фото (если она была).
/// </summary>
public sealed record DamageInspectionActModel
{
    public required string TrackingNumber { get; init; }
    public required DateTimeOffset InspectedAt { get; init; }

    public required string PackagingCondition { get; init; }
    public required string CargoCondition { get; init; }

    public string? OrderNumber { get; init; }
    public string? InspectedByName { get; init; }

    /// <summary>Комментарий сотрудника об обстоятельствах повреждения.</summary>
    public string? Comment { get; init; }

    /// <summary>
    /// Вердикт модели ai-inspection-service, если проверка проводилась: печатается как
    /// вспомогательное свидетельство, но не подменяет оценку сотрудника — акт подписывает он.
    /// </summary>
    public bool? AiDamageDetected { get; init; }
    public double? AiConfidence { get; init; }

    public int PhotoCount { get; init; }
}
