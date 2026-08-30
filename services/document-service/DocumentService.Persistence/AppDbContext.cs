using DocumentService.Domain.Entities;
using DocumentService.Persistence.Inbox;
using DocumentService.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace DocumentService.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();

    // Сведения о заявке для печати — read-модель по событиям orders-service.
    public DbSet<OrderSnapshot> OrderSnapshots => Set<OrderSnapshot>();

    // Отметки об обработанных событиях — дедупликация повторных доставок.
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    // Исходящие события (DocumentGenerated), ждущие публикации в RabbitMQ.
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
