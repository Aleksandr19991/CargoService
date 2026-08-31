using Microsoft.EntityFrameworkCore;
using PaymentService.Domain.Entities;
using PaymentService.Persistence.Inbox;
using PaymentService.Persistence.Outbox;

namespace PaymentService.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Refund> Refunds => Set<Refund>();

    // Отметки об обработанных событиях — дедупликация повторных доставок.
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    // Исходящие события (PaymentCompleted/PaymentFailed), ждущие публикации в RabbitMQ.
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
