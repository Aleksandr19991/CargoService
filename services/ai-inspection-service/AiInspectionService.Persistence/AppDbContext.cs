using AiInspectionService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiInspectionService.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<InspectionJob> InspectionJobs => Set<InspectionJob>();
    public DbSet<InspectionResult> InspectionResults => Set<InspectionResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
