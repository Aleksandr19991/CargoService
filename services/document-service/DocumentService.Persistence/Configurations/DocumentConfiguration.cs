using DocumentService.Domain.Entities;
using DocumentService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentService.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");

        builder.HasKey(document => document.Id);

        builder.Property(document => document.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(document => document.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(DocumentStatus.Pending)
            .IsRequired();

        builder.Property(document => document.ShipmentId).IsRequired();
        builder.Property(document => document.OrderId).IsRequired();

        builder.Property(document => document.TrackingNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(document => document.IssuedAt).IsRequired();
        builder.Property(document => document.CreatedAt).IsRequired();

        builder.Property(document => document.PackagingCondition).HasMaxLength(30);
        builder.Property(document => document.CargoCondition).HasMaxLength(30);
        builder.Property(document => document.ReceivedByName).HasMaxLength(200);
        builder.Property(document => document.FailureReason).HasMaxLength(1000);

        // Выборка фонового рабочего — «что осталось напечатать». Индекс частичный: готовые
        // документы составят почти всю таблицу и в этой выборке не нужны.
        builder.HasIndex(document => document.Status)
            .HasFilter("\"Status\" = 'Pending'");

        // Выдача документов по заявке и по грузу (задача 6) — два разных вопроса, и по заявке
        // спрашивают чаще: клиент знает номер заявки, а не трек-номер.
        builder.HasIndex(document => new { document.OrderId, document.CreatedAt });
        builder.HasIndex(document => document.ShipmentId);

        // Уникальности по (ShipmentId, Type) нет намеренно: акт приёма-передачи выпускается
        // дважды — на приёмке и на выдаче, — а повторный выпуск документа после исправления
        // данных это обычная жизнь бумаги. От дублей при повторной доставке события защищает
        // inbox, а не схема.
    }
}
