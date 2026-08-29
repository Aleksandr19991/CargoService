namespace AiInspectionService.API.Models.Responses;

/// <summary>
/// Отчёт о качестве модели на размеченном наборе. Отдаётся целиком по всем порогам: выбор
/// порога — решение бизнеса (что дороже: ложная тревога или пропущенное повреждение), и
/// отчёт показывает цену каждого варианта, а не только «лучший» по F1.
/// </summary>
public sealed record ModelEvaluationResponse
{
    public required string ModelVersion { get; init; }
    public required int SampleCount { get; init; }
    public required int DamagedSampleCount { get; init; }
    public required double RecommendedThreshold { get; init; }
    public required double CurrentThreshold { get; init; }
    public required List<ConfusionMatrixResponse> Thresholds { get; init; }
}

public sealed record ConfusionMatrixResponse
{
    public required double Threshold { get; init; }
    public required int TruePositives { get; init; }
    public required int FalsePositives { get; init; }
    public required int TrueNegatives { get; init; }
    public required int FalseNegatives { get; init; }
    public required double Precision { get; init; }
    public required double Recall { get; init; }
    public required double F1 { get; init; }
    public required double Accuracy { get; init; }
}
