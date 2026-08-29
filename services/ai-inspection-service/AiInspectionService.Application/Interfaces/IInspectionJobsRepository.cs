using AiInspectionService.Domain.Entities;

namespace AiInspectionService.Application.Interfaces;

public interface IInspectionJobsRepository
{
    Task CreateAsync(InspectionJob job, CancellationToken cancellationToken);

    Task<InspectionJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
