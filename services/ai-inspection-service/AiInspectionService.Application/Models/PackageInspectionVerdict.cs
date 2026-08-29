namespace AiInspectionService.Application.Models;

/// <summary>
/// Вердикт модели по одному снимку. Поля совпадают с тем, что ТЗ (§2.7) требует хранить в
/// <c>InspectionResult</c>, — сущность появится в следующей задаче Фазы 7.
/// </summary>
public sealed record PackageInspectionVerdict
{
    /// <summary>Оценка целостности упаковки, 0..1: 1 — «повреждений не видно».</summary>
    public required double PackagingIntegrityScore { get; init; }

    /// <summary>Вывод модели: повреждение видно. Это argmax по классам, а не сравнение со порогом.</summary>
    public required bool DamageDetected { get; init; }

    /// <summary>Уверенность в выбранном классе, 0..1.</summary>
    public required double Confidence { get; init; }

    /// <summary>
    /// Версия модели, которой получен вердикт. Хранится вместе с результатом, потому что
    /// результаты разных версий несравнимы: без неё нельзя ни разобрать инцидент, ни посчитать
    /// метрики качества по срезу (задача 8 Фазы 7).
    /// </summary>
    public required string ModelVersion { get; init; }

    /// <summary>Сырой ответ модели (JSON) — то же, что RawResponse в ТЗ: пригодится, когда вердикт оспаривают.</summary>
    public required string RawResponse { get; init; }
}
