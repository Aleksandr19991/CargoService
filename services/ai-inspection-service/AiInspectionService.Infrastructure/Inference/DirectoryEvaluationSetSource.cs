using AiInspectionService.Application.Interfaces;
using AiInspectionService.Application.Models;
using Microsoft.Extensions.Logging;

namespace AiInspectionService.Infrastructure.Inference;

/// <summary>
/// Размеченный тестовый набор с диска: подпапки <c>damaged/</c> и <c>intact/</c> внутри
/// <see cref="OnnxModelOptions.EvaluationSetPath"/>. Разметка — это имя папки, а не файл с
/// метками рядом: так набор пополняется перетаскиванием снимка мышью, чем и занимается
/// человек, который его размечает.
/// <para>
/// В репозиторий набор не кладётся (это чужие фотографии грузов) и в образ не попадает —
/// подключается томом, как и сама модель.
/// </para>
/// </summary>
public class DirectoryEvaluationSetSource(
    OnnxModelOptions options,
    ILogger<DirectoryEvaluationSetSource> logger) : IEvaluationSetSource
{
    private static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public async Task<IReadOnlyList<LabelledSample>> LoadAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.EvaluationSetPath) || !Directory.Exists(options.EvaluationSetPath))
        {
            logger.LogWarning(
                "AiModel:EvaluationSetPath is {State}, evaluation set not loaded",
                string.IsNullOrWhiteSpace(options.EvaluationSetPath) ? "not configured" : $"missing ({options.EvaluationSetPath})");

            return [];
        }

        var samples = new List<LabelledSample>();

        foreach (var (folder, isDamaged) in new[] { ("damaged", true), ("intact", false) })
        {
            var path = Path.Combine(options.EvaluationSetPath, folder);
            if (!Directory.Exists(path))
                continue;

            foreach (var file in Directory.EnumerateFiles(path).OrderBy(file => file))
            {
                if (!SupportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    continue;

                samples.Add(new LabelledSample
                {
                    Name = Path.GetFileName(file),
                    ImageBytes = await File.ReadAllBytesAsync(file, cancellationToken),
                    IsDamaged = isDamaged,
                });
            }
        }

        logger.LogInformation("Loaded {SampleCount} labelled samples from {Path}", samples.Count, options.EvaluationSetPath);

        return samples;
    }
}
