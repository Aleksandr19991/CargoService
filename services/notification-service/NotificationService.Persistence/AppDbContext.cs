using Microsoft.EntityFrameworkCore;
using NotificationService.Domain.Entities;
using NotificationService.Persistence.Inbox;

namespace NotificationService.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    // Read-модель «куда слать», наполняемая событиями UserRegistered и OrderCreated.
    public DbSet<NotificationRecipient> NotificationRecipients => Set<NotificationRecipient>();
    public DbSet<OrderRecipient> OrderRecipients => Set<OrderRecipient>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    // Отметки об обработанных событиях — дедупликация повторных доставок.
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
