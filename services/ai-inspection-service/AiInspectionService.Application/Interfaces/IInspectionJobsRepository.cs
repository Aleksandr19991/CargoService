using AiInspectionService.Domain.Entities;

namespace AiInspectionService.Application.Interfaces;

public interface IInspectionJobsRepository
{
    Task CreateAsync(InspectionJob job, CancellationToken cancellationToken);

    Task<InspectionJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Берёт самое старое задание в очереди и сразу переводит его в <c>Processing</c> — одной
    /// операцией, чтобы два экземпляра сервиса не взяли одно и то же задание. <c>null</c>, если
    /// очередь пуста.
    /// </summary>
    Task<InspectionJob?> ClaimNextQueuedAsync(CancellationToken cancellationToken);

    /// <summary>Записывает вердикты и закрывает задание — одной транзакцией.</summary>
    Task CompleteAsync(
        Guid jobId,
        IReadOnlyList<InspectionResult> results,
        CancellationToken cancellationToken);

    Task FailAsync(Guid jobId, string reason, CancellationToken cancellationToken);
}
