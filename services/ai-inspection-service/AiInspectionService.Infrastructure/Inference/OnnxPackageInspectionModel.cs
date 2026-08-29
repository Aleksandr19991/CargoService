using System.Text.Json;
using AiInspectionService.Application.Interfaces;
using AiInspectionService.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AiInspectionService.Infrastructure.Inference;

/// <summary>
/// Локальная ONNX-модель классификации «упаковка цела / повреждена».
/// <para>
/// <b>Контракт модели</b> (см. README сервиса — по нему же обучается или подбирается файл):
/// вход — один тензор <c>float32[1,3,224,224]</c>, RGB, каналы первыми, значения приведены к
/// [0,1] и нормализованы средним/дисперсией ImageNet; выход — один тензор <c>float32[1,2]</c>
/// с оценками классов в порядке <c>[intact, damaged]</c>. Логиты это или вероятности, модель
/// может не сообщать, поэтому softmax применяется всегда: на уже нормированном распределении
/// он монотонен и argmax не меняет.
/// </para>
/// <para>
/// Несоответствие контракту — отказ при загрузке (для формы входа/выхода, известной из
/// метаданных) или при первом вызове, с явным сообщением. Молча подстраиваться под чужую форму
/// нельзя: «1000 классов ImageNet» ничем не выдаст себя в вердикте, кроме бессмысленных
/// вероятностей повреждения.
/// </para>
/// </summary>
public sealed class OnnxPackageInspectionModel : IPackageInspectionModel, IDisposable
{
    private const int ImageSize = 224;
    private const int ClassCount = 2;
    private const int IntactClass = 0;
    private const int DamagedClass = 1;

    // Нормализация ImageNet — на ней обучены практически все переносимые классификаторы
    // изображений, от которых имеет смысл дообучать модель под упаковку.
    private static readonly float[] Mean = [0.485f, 0.456f, 0.406f];
    private static readonly float[] StdDev = [0.229f, 0.224f, 0.225f];

    private readonly InferenceSession session;
    private readonly string inputName;
    private readonly string outputName;

    public OnnxPackageInspectionModel(OnnxModelOptions options, ILogger<OnnxPackageInspectionModel> logger)
    {
        Version = options.Version;

        session = new InferenceSession(options.Path!);
        inputName = Single(session.InputMetadata.Keys, "input");
        outputName = Single(session.OutputMetadata.Keys, "output");

        ValidateShape(session.InputMetadata[inputName].Dimensions, [3, ImageSize, ImageSize], inputName, "входа");
        ValidateShape(session.OutputMetadata[outputName].Dimensions, [ClassCount], outputName, "выхода");

        logger.LogInformation(
            "ONNX model {Version} loaded from {Path} (input {Input}, output {Output})",
            Version, options.Path, inputName, outputName);
    }

    public string Version { get; }

    public Task<PackageInspectionVerdict> InspectAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var input = Preprocess(imageBytes);

        using var results = session.Run([NamedOnnxValue.CreateFromTensor(inputName, input)]);
        var scores = results.Single(value => value.Name == outputName).AsEnumerable<float>().ToArray();

        if (scores.Length != ClassCount)
        {
            throw new InvalidOperationException(
                $"Модель вернула {scores.Length} значений вместо {ClassCount} ([intact, damaged]).");
        }

        var probabilities = Softmax(scores);
        var damaged = probabilities[DamagedClass] > probabilities[IntactClass];

        return Task.FromResult(new PackageInspectionVerdict
        {
            PackagingIntegrityScore = probabilities[IntactClass],
            DamageDetected = damaged,
            Confidence = damaged ? probabilities[DamagedClass] : probabilities[IntactClass],
            ModelVersion = Version,
            RawResponse = JsonSerializer.Serialize(new
            {
                output = outputName,
                scores,
                probabilities,
            }),
        });
    }

    public void Dispose() => session.Dispose();

    /// <summary>
    /// Препроцессинг под контракт: декодирование, приведение к 224×224 и нормализация.
    /// Кадрирование по центру после вписывания по меньшей стороне, а не растяжение: у снимка
    /// со склада произвольные пропорции, и растяжение исказило бы геометрию коробки — ровно то,
    /// по чему модель и различает вмятину.
    /// </summary>
    private static DenseTensor<float> Preprocess(byte[] imageBytes)
    {
        using var image = Image.Load<Rgb24>(imageBytes);

        image.Mutate(context => context.Resize(new ResizeOptions
        {
            Size = new Size(ImageSize, ImageSize),
            Mode = ResizeMode.Crop,
        }));

        var tensor = new DenseTensor<float>([1, 3, ImageSize, ImageSize]);

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    tensor[0, 0, y, x] = (row[x].R / 255f - Mean[0]) / StdDev[0];
                    tensor[0, 1, y, x] = (row[x].G / 255f - Mean[1]) / StdDev[1];
                    tensor[0, 2, y, x] = (row[x].B / 255f - Mean[2]) / StdDev[2];
                }
            }
        });

        return tensor;
    }

    private static double[] Softmax(float[] scores)
    {
        // Вычитание максимума — обычная защита от переполнения экспоненты на логитах.
        var max = scores.Max();
        var exponentials = scores.Select(score => Math.Exp(score - max)).ToArray();
        var sum = exponentials.Sum();

        return exponentials.Select(value => value / sum).ToArray();
    }

    private static string Single(IEnumerable<string> names, string kind)
    {
        var list = names.ToList();
        if (list.Count != 1)
        {
            throw new InvalidOperationException(
                $"Модель должна иметь ровно один тензор {kind}, а имеет {list.Count}: {string.Join(", ", list)}.");
        }

        return list[0];
    }

    /// <summary>
    /// Сверяет форму без учёта первой оси: размер батча модели часто объявлен динамическим
    /// (-1) или единичным, и придираться к нему нечего — сервис всё равно шлёт по снимку.
    /// </summary>
    private static void ValidateShape(int[] dimensions, int[] expectedTail, string tensorName, string kind)
    {
        var tail = dimensions.Skip(1).ToArray();
        if (tail.Length != expectedTail.Length || !tail.SequenceEqual(expectedTail))
        {
            throw new InvalidOperationException(
                $"Форма {kind} «{tensorName}» = [{string.Join(", ", dimensions)}], " +
                $"а контракт требует [N, {string.Join(", ", expectedTail)}] " +
                "(см. докблок OnnxPackageInspectionModel и README сервиса).");
        }
    }
}
