using System.Text.Json;
using AiInspectionService.Application.Interfaces;
using AiInspectionService.Application.Models;
using AiInspectionService.Domain.Entities;
using CargoService.Contracts.Events.V1;
using CargoService.Contracts.Messaging;
using Microsoft.Extensions.Logging;

namespace AiInspectionService.Application;

/// <summary>
/// Конвейер инференса: задание из очереди → снимки из File Storage → модель → вердикты →
/// событие <c>PackageIntegrityAssessed</c> через outbox.
/// </summary>
public class InspectionProcessor(
    IInspectionJobsRepository jobsRepository,
    IFileStorageClient fileStorageClient,
    IPackageInspectionModel model,
    IOutboxWriter outboxWriter,
    InspectionOptions options,
    ILogger<InspectionProcessor> logger) : IInspectionProcessor
{
    private const string PublishingService = "ai-inspection-service";
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var job = await jobsRepository.ClaimNextQueuedAsync(cancellationToken);
        if (job is null)
            return false;

        logger.LogInformation(
            "Inspecting job {JobId} for shipment {ShipmentId}: {PhotoCount} photo(s), model {ModelVersion}",
            job.Id, job.ShipmentId, job.PhotoFileIds.Count, model.Version);

        try
        {
            var results = new List<InspectionResult>();

            foreach (var fileId in job.PhotoFileIds)
            {
                var imageBytes = await fileStorageClient.DownloadAsync(fileId, cancellationToken);
                if (imageBytes is null)
                {
                    // Недостающий снимок обрывает задание целиком, а не пропускается: вердикт по
                    // части фотографий не отличить от вердикта по всем, а «повреждений не видно»
                    // на невиденных снимках — ровно та ошибка, ради которой проверку и заводили.
                    // Событие при этом не публикуется: вердикта нет, а cargo-service записал бы
                    // в акт приёмки оценку, ни на чём не основанную.
                    await FailAsync(job.Id, $"Снимок {fileId} отсутствует в хранилище.", cancellationToken);
                    return true;
                }

                var verdict = await model.InspectAsync(imageBytes, cancellationToken);

                results.Add(new InspectionResult
                {
                    FileId = fileId,
                    PackagingIntegrityScore = verdict.PackagingIntegrityScore,
                    DamageDetected = verdict.DamageDetected,
                    Confidence = verdict.Confidence,
                    ModelVersion = verdict.ModelVersion,
                    RawResponse = verdict.RawResponse,
                    AssessedAt = DateTimeOffset.UtcNow,
                });
            }

            // Событие ставится в outbox до сохранения: репозиторий делает SaveChanges на том же
            // DbContext, поэтому вердикты, статус задания и строка outbox коммитятся одной
            // транзакцией — как в остальных сервисах с outbox (Фазы 4–6).
            var assessment = Aggregate(job, results);
            outboxWriter.Enqueue(
                assessment.EventId,
                RabbitMqConventions.RoutingKey(PublishingService, nameof(PackageIntegrityAssessed)),
                JsonSerializer.Serialize(assessment),
                assessment.OccurredAtUtc);

            await jobsRepository.CompleteAsync(job.Id, results, cancellationToken);

            logger.LogInformation(
                "Job {JobId} completed: damage on {DamagedCount} of {PhotoCount} photo(s), " +
                "published verdict damage={Damage} confidence={Confidence:F3}",
                job.Id,
                results.Count(result => result.DamageDetected),
                results.Count,
                assessment.DamageDetected,
                assessment.Confidence);

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Остановка сервиса: задание останется в Processing и будет подхвачено при
            // следующем запуске — ручным перезапуском (задача 7); помечать его Failed нельзя,
            // это не отказ.
            throw;
        }
        catch (Exception exception)
        {
            // Сюда попадают отказ хранилища, битый файл, ошибка модели. Задание закрывается
            // как Failed с причиной, а не бросает исключение наверх: рабочий цикл должен
            // продолжить разбирать очередь, а не падать на одном плохом снимке.
            logger.LogError(exception, "Job {JobId} failed", job.Id);
            await FailAsync(job.Id, Truncate(exception.Message), CancellationToken.None);

            return true;
        }
    }

    /// <summary>
    /// Сводит вердикты по снимкам в один — тот, что уходит подписчикам.
    /// <para>
    /// Повреждение считается найденным, если оно видно **хоть на одном** снимке и модель уверена
    /// не ниже порога (<see cref="InspectionOptions.DamageConfidenceThreshold"/>): фотографируют
    /// разные стороны коробки, и вмятина на одной из них — это повреждение груза, а не
    /// «меньшинство голосов»; но догадка с уверенностью чуть выше половины — не повреждение.
    /// </para>
    /// <para>
    /// Уверенность берётся у снимка, решившего исход: при найденном повреждении — наибольшая
    /// среди сработавших, иначе — наименьшая оценка целостности по всем снимкам. Второе
    /// намеренно осторожно и учитывает слабые сигналы повреждения, не дотянувшие до порога:
    /// вердикт «цело» настолько надёжен, насколько надёжен худший снимок.
    /// </para>
    /// </summary>
    private PackageIntegrityAssessed Aggregate(InspectionJob job, IReadOnlyList<InspectionResult> results)
    {
        var damaged = results
            .Where(result => result.DamageDetected && result.Confidence >= options.DamageConfidenceThreshold)
            .ToList();

        return new PackageIntegrityAssessed
        {
            ShipmentId = job.ShipmentId,
            InspectionJobId = job.Id,
            DamageDetected = damaged.Count > 0,
            Confidence = damaged.Count > 0
                ? damaged.Max(result => result.Confidence)
                : results.Min(result => result.PackagingIntegrityScore),
        };
    }

    private async Task FailAsync(Guid jobId, string reason, CancellationToken cancellationToken)
    {
        logger.LogWarning("Job {JobId} failed: {Reason}", jobId, reason);
        await jobsRepository.FailAsync(jobId, reason, cancellationToken);
    }

    // Колонка FailureReason ограничена 1000 символами, а сообщения провайдеров бывают длиннее.
    private static string Truncate(string value) => value.Length <= 1000 ? value : value[..1000];
}
