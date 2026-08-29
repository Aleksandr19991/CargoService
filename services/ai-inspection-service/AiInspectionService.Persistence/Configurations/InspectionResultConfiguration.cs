using AiInspectionService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiInspectionService.Persistence.Configurations;

public class InspectionResultConfiguration : IEntityTypeConfiguration<InspectionResult>
{
    public void Configure(EntityTypeBuilder<InspectionResult> builder)
    {
        builder.ToTable("inspection_results");

        builder.HasKey(result => result.Id);

        builder.Property(result => result.JobId)
            .IsRequired();

        builder.Property(result => result.FileId)
            .IsRequired();

        builder.Property(result => result.PackagingIntegrityScore)
            .IsRequired();

        builder.Property(result => result.DamageDetected)
            .IsRequired();

        builder.Property(result => result.Confidence)
            .IsRequired();

        // Нативная колонка text[]: список коротких меток, который читается целиком вместе с
        // вердиктом — та же логика, что у PhotoFileIds задания.
        builder.Property(result => result.DamageTypes)
            .IsRequired();

        builder.Property(result => result.ModelVersion)
            .HasMaxLength(100)
            .IsRequired();

        // Без ограничения длины (Postgres text): это ответ модели как есть, и обрезать его
        // значило бы хранить не то, что она вернула.
        builder.Property(result => result.RawResponse)
            .IsRequired();

        builder.Property(result => result.AssessedAt)
            .IsRequired();

        // Один вердикт на снимок в пределах задания: повтор внутри одного задания — это
        // ошибка обработчика (например, задание взяли в работу дважды), и лучше узнать о ней
        // от БД, чем считать метрики по задвоенным строкам.
        builder.HasIndex(result => new { result.JobId, result.FileId })
            .IsUnique();
    }
}
