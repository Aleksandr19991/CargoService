using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Persistence.Configurations;

public class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.ToTable("notification_logs");

        builder.HasKey(log => log.Id);

        builder.Property(log => log.RecipientContact)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(log => log.Channel)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(log => log.TemplateCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(log => log.Subject)
            .HasMaxLength(200);

        // Как и Body шаблона — без ограничения длины: это отправленный текст, обрезать его
        // в истории значило бы хранить не то, что ушло получателю.
        builder.Property(log => log.Body)
            .IsRequired();

        builder.Property(log => log.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(NotificationStatus.Pending)
            .IsRequired();

        builder.Property(log => log.CreatedAt)
            .IsRequired();

        builder.Property(log => log.FailureReason)
            .HasMaxLength(1000);

        builder.Property(log => log.RelatedEntityType)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Личный кабинет читает историю одного клиента, свежие сверху (GET /notifications,
        // задача 6). Индекс частичный: уведомления сотрудникам пользователя не имеют и в
        // клиентскую выборку не попадают.
        builder.HasIndex(log => new { log.RecipientUserId, log.CreatedAt })
            .HasFilter("\"RecipientUserId\" IS NOT NULL");
    }
}
