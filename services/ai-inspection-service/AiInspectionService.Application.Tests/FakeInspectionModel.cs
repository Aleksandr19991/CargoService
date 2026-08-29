using System.Text;
using AiInspectionService.Application.Interfaces;
using AiInspectionService.Application.Models;

namespace AiInspectionService.Application.Tests;

/// <summary>
/// Мок модели: вердикт задаётся по содержимому снимка, чтобы тест мог собрать любую комбинацию
/// «повреждено / цело / какая уверенность», не имея файла модели. Настоящий инференс проверять
/// unit-тестом бессмысленно — там проверяется контракт ONNX-адаптера, а не бизнес-правила.
/// </summary>
public class FakeInspectionModel(string version = "fake-1.0") : IPackageInspectionModel
{
    private readonly Dictionary<string, (bool Damaged, double Confidence)> verdicts = [];

    public string Version { get; } = version;

    public int InspectCallCount { get; private set; }

    public static byte[] Image(string name) => Encoding.UTF8.GetBytes(name);

    public FakeInspectionModel WithVerdict(string imageName, bool damaged, double confidence)
    {
        verdicts[imageName] = (damaged, confidence);
        return this;
    }

    public Task<PackageInspectionVerdict> InspectAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        InspectCallCount++;

        var name = Encoding.UTF8.GetString(imageBytes);
        var (damaged, confidence) = verdicts.TryGetValue(name, out var verdict)
            ? verdict
            : (false, 0.99);

        return Task.FromResult(new PackageInspectionVerdict
        {
            // Оценка целостности и уверенность связаны через два класса: у «повреждено»
            // целостность — это остаток вероятности.
            PackagingIntegrityScore = damaged ? 1 - confidence : confidence,
            DamageDetected = damaged,
            Confidence = confidence,
            ModelVersion = Version,
            RawResponse = $$"""{"fake":"{{name}}"}""",
        });
    }
}
