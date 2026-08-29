using AiInspectionService.Domain.Entities;
using AiInspectionService.Persistence.Inbox;
using Microsoft.EntityFrameworkCore;

namespace AiInspectionService.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<InspectionJob> InspectionJobs => Set<InspectionJob>();
    public DbSet<InspectionResult> InspectionResults => Set<InspectionResult>();

    // Отметки об обработанных событиях — дедупликация повторных доставок CargoPhotoUploaded.
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
