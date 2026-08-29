using AiInspectionService.Domain.Entities;
using AiInspectionService.Persistence.Inbox;
using AiInspectionService.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace AiInspectionService.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<InspectionJob> InspectionJobs => Set<InspectionJob>();
    public DbSet<InspectionResult> InspectionResults => Set<InspectionResult>();

    // Отметки об обработанных событиях — дедупликация повторных доставок CargoPhotoUploaded.
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    // Исходящие события (PackageIntegrityAssessed), ждущие публикации в RabbitMQ.
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
