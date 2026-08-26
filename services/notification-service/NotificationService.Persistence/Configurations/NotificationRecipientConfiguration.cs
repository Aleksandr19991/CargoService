using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationService.Domain.Entities;

namespace NotificationService.Persistence.Configurations;

public class NotificationRecipientConfiguration : IEntityTypeConfiguration<NotificationRecipient>
{
    public void Configure(EntityTypeBuilder<NotificationRecipient> builder)
    {
        builder.ToTable("notification_recipients");

        // Ключ — идентификатор пользователя из identity-service: своего суррогатного ключа у
        // read-модели нет, а естественный уже уникален и приезжает в каждом событии.
        builder.HasKey(recipient => recipient.UserId);

        builder.Property(recipient => recipient.UserId)
            .ValueGeneratedNever();

        builder.Property(recipient => recipient.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(recipient => recipient.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(recipient => recipient.Email)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(recipient => recipient.Phone)
            .HasMaxLength(50)
            .IsRequired();
    }
}
