using AiInspectionService.Application.Interfaces;
using AiInspectionService.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AiInspectionService.Application;

/// <summary>
/// Конвейер инференса: задание из очереди → снимки из File Storage → модель → вердикты.
/// <para>
/// Публикация <c>PackageIntegrityAssessed</c> сюда ещё не входит — она будет добавлена
/// следующей задачей Фазы 7 вместе с outbox.
/// </para>
/// </summary>
public class InspectionProcessor(
    IInspectionJobsRepository jobsRepository,
    IFileStorageClient fileStorageClient,
    IPackageInspectionModel model,
    ILogger<InspectionProcessor> logger) : IInspectionProcessor
{
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

            await jobsRepository.CompleteAsync(job.Id, results, cancellationToken);

            logger.LogInformation(
                "Job {JobId} completed: damage on {DamagedCount} of {PhotoCount} photo(s)",
                job.Id, results.Count(result => result.DamageDetected), results.Count);

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

    private async Task FailAsync(Guid jobId, string reason, CancellationToken cancellationToken)
    {
        logger.LogWarning("Job {JobId} failed: {Reason}", jobId, reason);
        await jobsRepository.FailAsync(jobId, reason, cancellationToken);
    }

    // Колонка FailureReason ограничена 1000 символами, а сообщения провайдеров бывают длиннее.
    private static string Truncate(string value) => value.Length <= 1000 ? value : value[..1000];
}
