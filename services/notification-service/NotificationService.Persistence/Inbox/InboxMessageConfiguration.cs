using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NotificationService.Persistence.Inbox;

public class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");

        builder.HasKey(message => message.EventId);

        builder.Property(message => message.EventId)
            .ValueGeneratedNever();

        builder.Property(message => message.EventType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(message => message.ProcessedAtUtc)
            .IsRequired();
    }
}
