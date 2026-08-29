using AiInspectionService.Application.Interfaces;
using AiInspectionService.Domain.Entities;
using AiInspectionService.Domain.Enums;

namespace AiInspectionService.Application;

public class InspectionJobsService(IInspectionJobsRepository jobsRepository) : IInspectionJobsService
{
    public async Task<InspectionJob?> EnqueueAsync(
        Guid shipmentId,
        IReadOnlyCollection<Guid> photoFileIds,
        CancellationToken cancellationToken)
    {
        // Повторы внутри одного события отсеиваем здесь: одинаковые снимки в задании нарушили бы
        // уникальность (JobId, FileId) уже на записи вердиктов — то есть после инференса, когда
        // время на них потрачено.
        var photos = photoFileIds.Distinct().ToList();
        if (photos.Count == 0)
            return null;

        var job = new InspectionJob
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipmentId,
            PhotoFileIds = photos,
            Status = InspectionJobStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await jobsRepository.CreateAsync(job, cancellationToken);

        return job;
    }

    public Task<InspectionJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        jobsRepository.GetByIdAsync(id, cancellationToken);
}
