namespace AiInspectionService.Application.Models;

/// <summary>
/// Качество модели на размеченном наборе: матрица ошибок для нескольких порогов уверенности и
/// рекомендуемый порог (spec.md §5, Фаза 7).
/// </summary>
public sealed record ModelEvaluationReport
{
    public required string ModelVersion { get; init; }

    /// <summary>Сколько снимков в наборе и сколько из них размечены как повреждённые.</summary>
    public required int SampleCount { get; init; }
    public required int DamagedSampleCount { get; init; }

    public required IReadOnlyList<ConfusionMatrix> Thresholds { get; init; }

    /// <summary>
    /// Порог с наибольшей F1-мерой. F1 выбрана как нейтральный ориентир: она не даёт ни
    /// заваливать склад ложными тревогами, ни пропускать повреждения. Бизнес вправе выбрать
    /// другой порог из той же таблицы — например, с высокой точностью, если ложная тревога
    /// дороже пропуска.
    /// </summary>
    public required double RecommendedThreshold { get; init; }
}

/// <summary>
/// Матрица ошибок при одном пороге. «Положительный» класс — повреждение: пропущенное
/// повреждение (FN) и ложная тревога (FP) стоят разного, поэтому обе величины показываются
/// отдельно, а не сворачиваются в одну «точность».
/// </summary>
public sealed record ConfusionMatrix
{
    public required double Threshold { get; init; }

    public required int TruePositives { get; init; }
    public required int FalsePositives { get; init; }
    public required int TrueNegatives { get; init; }
    public required int FalseNegatives { get; init; }

    public double Precision => TruePositives + FalsePositives == 0
        ? 0
        : (double)TruePositives / (TruePositives + FalsePositives);

    public double Recall => TruePositives + FalseNegatives == 0
        ? 0
        : (double)TruePositives / (TruePositives + FalseNegatives);

    public double F1 => Precision + Recall == 0
        ? 0
        : 2 * Precision * Recall / (Precision + Recall);

    public double Accuracy => TruePositives + TrueNegatives + FalsePositives + FalseNegatives == 0
        ? 0
        : (double)(TruePositives + TrueNegatives) / (TruePositives + TrueNegatives + FalsePositives + FalseNegatives);
}
