using AiInspectionService.Application.Interfaces;
using AiInspectionService.Application.Models;
using Microsoft.Extensions.Logging;

namespace AiInspectionService.Application;

/// <summary>
/// Считает качество модели на размеченном наборе: по матрице ошибок на каждый порог уверенности.
/// <para>
/// Перебор порогов, а не одна цифра, — потому что выбор порога это бизнес-решение, а не
/// свойство модели: ложная тревога стоит рабочего времени склада, пропущенное повреждение —
/// спора с клиентом, и чему из этого отдать предпочтение, решают не здесь. Отчёт показывает
/// цену каждого варианта.
/// </para>
/// </summary>
public class ModelEvaluator(
    IEvaluationSetSource evaluationSetSource,
    IPackageInspectionModel model,
    ILogger<ModelEvaluator> logger) : IModelEvaluator
{
    /// <summary>
    /// Пороги, по которым строится отчёт. Ниже 0.5 нет смысла: у двух классов уверенность
    /// выбранного класса всегда не меньше половины.
    /// </summary>
    private static readonly double[] Thresholds = [0.5, 0.6, 0.7, 0.8, 0.9, 0.95];

    public async Task<ModelEvaluationReport?> EvaluateAsync(CancellationToken cancellationToken)
    {
        var samples = await evaluationSetSource.LoadAsync(cancellationToken);
        if (samples.Count == 0)
        {
            logger.LogWarning("Evaluation set is empty or not configured, nothing to measure");
            return null;
        }

        // Инференс делается один раз на снимок, а пороги применяются к уже полученным
        // вердиктам: гонять модель по разу на каждый порог значило бы умножить время отчёта
        // на число порогов, ничего не узнав дополнительно.
        var verdicts = new List<(bool IsDamaged, bool ModelSaysDamaged, double Confidence)>(samples.Count);

        foreach (var sample in samples)
        {
            var verdict = await model.InspectAsync(sample.ImageBytes, cancellationToken);
            verdicts.Add((sample.IsDamaged, verdict.DamageDetected, verdict.Confidence));
        }

        var matrices = Thresholds.Select(threshold => Measure(verdicts, threshold)).ToList();

        var report = new ModelEvaluationReport
        {
            ModelVersion = model.Version,
            SampleCount = samples.Count,
            DamagedSampleCount = samples.Count(sample => sample.IsDamaged),
            Thresholds = matrices,
            RecommendedThreshold = matrices.MaxBy(matrix => matrix.F1)!.Threshold,
        };

        logger.LogInformation(
            "Evaluated model {ModelVersion} on {SampleCount} samples, recommended threshold {Threshold}",
            report.ModelVersion, report.SampleCount, report.RecommendedThreshold);

        return report;
    }

    /// <summary>
    /// Повреждение засчитывается, только если модель назвала его и уверена не ниже порога:
    /// вердикт «повреждено» с уверенностью 0.51 — это догадка, и на складе от неё вреда
    /// больше, чем пользы.
    /// </summary>
    private static ConfusionMatrix Measure(
        IReadOnlyList<(bool IsDamaged, bool ModelSaysDamaged, double Confidence)> verdicts,
        double threshold)
    {
        var predictions = verdicts
            .Select(verdict => (verdict.IsDamaged, Predicted: verdict.ModelSaysDamaged && verdict.Confidence >= threshold))
            .ToList();

        return new ConfusionMatrix
        {
            Threshold = threshold,
            TruePositives = predictions.Count(prediction => prediction is { IsDamaged: true, Predicted: true }),
            FalsePositives = predictions.Count(prediction => prediction is { IsDamaged: false, Predicted: true }),
            TrueNegatives = predictions.Count(prediction => prediction is { IsDamaged: false, Predicted: false }),
            FalseNegatives = predictions.Count(prediction => prediction is { IsDamaged: true, Predicted: false }),
        };
    }
}
