using AiInspectionService.Application.Interfaces;
using AiInspectionService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiInspectionService.Persistence.Repositories;

public class InspectionJobsRepository(AppDbContext dbContext) : IInspectionJobsRepository
{
    public async Task CreateAsync(InspectionJob job, CancellationToken cancellationToken)
    {
        await dbContext.InspectionJobs.AddAsync(job, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<InspectionJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.InspectionJobs
            .AsNoTracking()
            .Include(job => job.Results)
            .FirstOrDefaultAsync(job => job.Id == id, cancellationToken);
}
