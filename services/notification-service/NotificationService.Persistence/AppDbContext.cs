using Microsoft.EntityFrameworkCore;
using NotificationService.Domain.Entities;

namespace NotificationService.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    // Read-модель «куда слать», наполняемая событиями UserRegistered и OrderCreated.
    public DbSet<NotificationRecipient> NotificationRecipients => Set<NotificationRecipient>();
    public DbSet<OrderRecipient> OrderRecipients => Set<OrderRecipient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
