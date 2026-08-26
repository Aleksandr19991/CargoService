using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationService.Domain.Entities;

namespace NotificationService.Persistence.Configurations;

public class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences");

        builder.HasKey(preference => preference.UserId);

        builder.Property(preference => preference.UserId)
            .ValueGeneratedNever();

        builder.Property(preference => preference.EmailEnabled)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(preference => preference.SmsEnabled)
            .HasDefaultValue(true)
            .IsRequired();
    }
}
