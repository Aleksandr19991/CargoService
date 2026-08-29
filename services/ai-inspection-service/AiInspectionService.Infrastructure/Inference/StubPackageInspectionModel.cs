using System.Security.Cryptography;
using System.Text.Json;
using AiInspectionService.Application.Interfaces;
using AiInspectionService.Application.Models;
using Microsoft.Extensions.Logging;

namespace AiInspectionService.Infrastructure.Inference;

/// <summary>
/// Заглушка модели на время, пока файл модели не подключён (см. <see cref="OnnxModelOptions"/>).
/// <para>
/// Это <b>не предсказание</b>: вердикт выводится из хэша снимка, а не из его содержимого.
/// Заглушка нужна затем, чтобы остальной конвейер — consumer <c>CargoPhotoUploaded</c>, задания,
/// публикация <c>PackageIntegrityAssessed</c>, API — оставался запускаемым и проверяемым без
/// модели, и по тем же соображениям, что <c>LoggingSmsSender</c> в notification-service: падать
/// на старте из-за неподключённой интеграции хуже, чем работать в явно помеченном режиме.
/// </para>
/// <para>
/// Вердикт детерминирован по байтам снимка: один и тот же файл всегда даёт один и тот же ответ,
/// иначе повторный прогон задания «чинил» бы или «ломал» результат случайным образом. Версия
/// модели — <c>stub</c>, так что в истории такие вердикты видно и их легко отделить при подсчёте
/// метрик качества (задача 8).
/// </para>
/// </summary>
public sealed class StubPackageInspectionModel(ILogger<StubPackageInspectionModel> logger) : IPackageInspectionModel
{
    public const string StubVersion = "stub";

    public string Version => StubVersion;

    public Task<PackageInspectionVerdict> InspectAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        var hash = SHA256.HashData(imageBytes);

        // Первые два байта хэша → «оценка целостности» в [0,1). Порог 0.5 делит вердикты
        // примерно поровну, чтобы обе ветки конвейера (повреждение есть / нет) встречались.
        var score = ((hash[0] << 8) | hash[1]) / 65536.0;
        var damaged = score < 0.5;

        logger.LogWarning(
            "ONNX model is not configured; stub verdict for image {Hash} — damage={Damage}, score={Score:F3}",
            Convert.ToHexString(hash)[..12], damaged, score);

        return Task.FromResult(new PackageInspectionVerdict
        {
            PackagingIntegrityScore = score,
            DamageDetected = damaged,
            Confidence = damaged ? 1 - score : score,
            ModelVersion = StubVersion,
            RawResponse = JsonSerializer.Serialize(new { stub = true, imageSha256 = Convert.ToHexString(hash), score }),
        });
    }
}
