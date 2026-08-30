namespace DocumentService.Application.Models;

/// <summary>
/// Данные акта осмотра при повреждении — документа, на который клиент ссылается в претензии.
/// Поэтому здесь важны не только состояния, но и то, когда повреждение зафиксировано и по какой
/// заявке.
/// </summary>
public sealed record DamageInspectionActModel
{
    public required string TrackingNumber { get; init; }
    public required DateTimeOffset InspectedAt { get; init; }

    public required string PackagingCondition { get; init; }
    public required string CargoCondition { get; init; }

    public string? OrderNumber { get; init; }
    public string? SenderName { get; init; }
    public string? RecipientName { get; init; }
    public string? CargoName { get; init; }
    public decimal? DeclaredValue { get; init; }

    /// <summary>
    /// Вердикт модели ai-inspection-service, если проверка проводилась: печатается как
    /// вспомогательное свидетельство, но не подменяет оценку сотрудника — акт подписывает он.
    /// Сегодня не заполняется: событие приёмки вердикта не несёт, а слушать
    /// <c>PackageIntegrityAssessed</c> ради строки в акте значит ждать проверку перед печатью.
    /// </summary>
    public bool? AiDamageDetected { get; init; }
    public double? AiConfidence { get; init; }
}
