using AiInspectionService.Application.Interfaces;
using AiInspectionService.Application.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AiInspectionService.Application.Tests;

public class ModelEvaluatorTests
{
    private readonly Mock<IEvaluationSetSource> source = new();
    private readonly FakeInspectionModel model = new();

    [Fact]
    public async Task EvaluateAsync_ReturnsNullWhenSetIsEmpty()
    {
        // Набора нет — считать не по чему; это отсутствие данных, а не сбой.
        source.Setup(instance => instance.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var report = await CreateEvaluator().EvaluateAsync(CancellationToken.None);

        Assert.Null(report);
    }

    [Fact]
    public async Task EvaluateAsync_CountsConfusionMatrixPerThreshold()
    {
        // Набор подобран так, чтобы порог 0.8 менял картину: уверенное повреждение остаётся,
        // неуверенное перестаёт считаться обнаружением.
        WithSamples(
            ("sure-damage", true, damaged: true, confidence: 0.95),
            ("weak-damage", true, damaged: true, confidence: 0.65),
            ("missed-damage", true, damaged: false, confidence: 0.90),
            ("false-alarm", false, damaged: true, confidence: 0.85),
            ("calm", false, damaged: false, confidence: 0.99));

        var report = await CreateEvaluator().EvaluateAsync(CancellationToken.None);

        Assert.NotNull(report);
        Assert.Equal(5, report.SampleCount);
        Assert.Equal(3, report.DamagedSampleCount);
        Assert.Equal("fake-1.0", report.ModelVersion);

        var atHalf = report.Thresholds.Single(matrix => matrix.Threshold == 0.5);
        Assert.Equal(2, atHalf.TruePositives);
        Assert.Equal(1, atHalf.FalsePositives);
        Assert.Equal(1, atHalf.TrueNegatives);
        Assert.Equal(1, atHalf.FalseNegatives);

        var atEighty = report.Thresholds.Single(matrix => matrix.Threshold == 0.8);
        Assert.Equal(1, atEighty.TruePositives);
        Assert.Equal(1, atEighty.FalsePositives);
        Assert.Equal(1, atEighty.TrueNegatives);
        Assert.Equal(2, atEighty.FalseNegatives);

        var atNinetyFive = report.Thresholds.Single(matrix => matrix.Threshold == 0.95);
        Assert.Equal(1, atNinetyFive.TruePositives);
        Assert.Equal(0, atNinetyFive.FalsePositives);
        Assert.Equal(2, atNinetyFive.TrueNegatives);
        Assert.Equal(2, atNinetyFive.FalseNegatives);

        // Модель прогоняется по разу на снимок, а не на каждый порог.
        Assert.Equal(5, model.InspectCallCount);
    }

    [Fact]
    public async Task EvaluateAsync_ComputesPrecisionRecallAndRecommendsBestF1()
    {
        WithSamples(
            ("sure-damage", true, damaged: true, confidence: 0.95),
            ("weak-damage", true, damaged: true, confidence: 0.65),
            ("false-alarm", false, damaged: true, confidence: 0.65),
            ("calm", false, damaged: false, confidence: 0.99));

        var report = await CreateEvaluator().EvaluateAsync(CancellationToken.None);

        Assert.NotNull(report);

        var atHalf = report.Thresholds.Single(matrix => matrix.Threshold == 0.5);
        Assert.Equal(2.0 / 3, atHalf.Precision, 6);
        Assert.Equal(1.0, atHalf.Recall, 6);
        Assert.Equal(0.8, atHalf.F1, 6);
        Assert.Equal(0.75, atHalf.Accuracy, 6);

        // При 0.9 остаётся одно верное обнаружение без ложных: точность 1, полнота 0.5, F1 ≈ 0.67 —
        // меньше, чем 0.8 у порога 0.5, поэтому рекомендуется именно он.
        var atNinety = report.Thresholds.Single(matrix => matrix.Threshold == 0.9);
        Assert.Equal(1.0, atNinety.Precision, 6);
        Assert.Equal(0.5, atNinety.Recall, 6);
        Assert.Equal(0.5, report.RecommendedThreshold);
    }

    private ModelEvaluator CreateEvaluator() =>
        new(source.Object, model, NullLogger<ModelEvaluator>.Instance);

    private void WithSamples(params (string Name, bool Label, bool damaged, double confidence)[] samples)
    {
        foreach (var sample in samples)
            model.WithVerdict(sample.Name, sample.damaged, sample.confidence);

        source.Setup(instance => instance.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(samples
                .Select(sample => new LabelledSample
                {
                    Name = sample.Name,
                    ImageBytes = FakeInspectionModel.Image(sample.Name),
                    IsDamaged = sample.Label,
                })
                .ToList());
    }
}
