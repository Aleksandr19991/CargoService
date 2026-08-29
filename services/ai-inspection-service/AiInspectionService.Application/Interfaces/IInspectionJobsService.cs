using AiInspectionService.Domain.Entities;

namespace AiInspectionService.Application.Interfaces;

public interface IInspectionJobsService
{
    /// <summary>
    /// Ставит задание на проверку снимков груза. Возвращает <c>null</c>, если ставить нечего —
    /// снимков в запросе нет; сам факт события с пустым списком не ошибка, но и задания без
    /// единого снимка не бывает.
    /// </summary>
    Task<InspectionJob?> EnqueueAsync(
        Guid shipmentId,
        IReadOnlyCollection<Guid> photoFileIds,
        CancellationToken cancellationToken);

    Task<InspectionJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
