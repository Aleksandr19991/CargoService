using AiInspectionService.Application.Interfaces;
using AiInspectionService.Domain.Entities;
using AiInspectionService.Domain.Enums;
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

    /// <summary>
    /// Захват задания сделан одним SQL с <c>FOR UPDATE SKIP LOCKED</c>, а не «прочитали →
    /// обновили» на стороне сервиса: второй способ отдал бы одно задание двум экземплярам,
    /// которые опросили очередь одновременно, и оба потратили бы инференс на одни снимки.
    /// <c>SKIP LOCKED</c> при этом не заставляет второй экземпляр ждать — он просто берёт
    /// следующее задание.
    /// </summary>
    public async Task<InspectionJob?> ClaimNextQueuedAsync(CancellationToken cancellationToken)
    {
        var jobs = await dbContext.InspectionJobs
            .FromSql(
                $"""
                 UPDATE inspection_jobs
                 SET "Status" = 'Processing', "StartedAt" = now()
                 WHERE "Id" = (
                     SELECT "Id" FROM inspection_jobs
                     WHERE "Status" = 'Queued'
                     ORDER BY "CreatedAt"
                     FOR UPDATE SKIP LOCKED
                     LIMIT 1
                 )
                 RETURNING *
                 """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return jobs.FirstOrDefault();
    }

    public async Task CompleteAsync(
        Guid jobId,
        IReadOnlyList<InspectionResult> results,
        CancellationToken cancellationToken)
    {
        var job = await dbContext.InspectionJobs.FirstAsync(stored => stored.Id == jobId, cancellationToken);

        foreach (var result in results)
        {
            // Ключ дочерней строки отдан EF: проставленный вручную Id на уже отслеживаемом
            // родителе EF принимает за существующую строку и генерирует UPDATE вместо INSERT
            // (эта ловушка ловилась живым прогоном в Фазе 5).
            result.JobId = jobId;
            job.Results.Add(result);
        }

        job.Status = InspectionJobStatus.Completed;
        job.CompletedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(Guid jobId, string reason, CancellationToken cancellationToken)
    {
        var job = await dbContext.InspectionJobs.FirstAsync(stored => stored.Id == jobId, cancellationToken);

        job.Status = InspectionJobStatus.Failed;
        job.CompletedAt = DateTimeOffset.UtcNow;
        job.FailureReason = reason;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
