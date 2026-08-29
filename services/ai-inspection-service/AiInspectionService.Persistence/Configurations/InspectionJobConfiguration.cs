using AiInspectionService.Domain.Entities;
using AiInspectionService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiInspectionService.Persistence.Configurations;

public class InspectionJobConfiguration : IEntityTypeConfiguration<InspectionJob>
{
    public void Configure(EntityTypeBuilder<InspectionJob> builder)
    {
        builder.ToTable("inspection_jobs");

        builder.HasKey(job => job.Id);

        builder.Property(job => job.ShipmentId)
            .IsRequired();

        builder.Property(job => job.PhotoFileIds)
            .IsRequired();

        builder.Property(job => job.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(InspectionJobStatus.Queued)
            .IsRequired();

        builder.Property(job => job.CreatedAt)
            .IsRequired();

        builder.Property(job => job.FailureReason)
            .HasMaxLength(1000);

        // История проверок одного груза: и карточка сотрудника, и повторный запуск смотрят
        // задания по грузу, свежие первыми.
        builder.HasIndex(job => new { job.ShipmentId, job.CreatedAt });

        // Выборка обработчика — «что взять в работу». Индекс частичный: завершённые и упавшие
        // задания составляют почти всю таблицу и в этой выборке не нужны.
        builder.HasIndex(job => job.Status)
            .HasFilter("\"Status\" = 'Queued'");

        // Уникальности по ShipmentId сознательно нет (в отличие от Shipment.OrderId в
        // cargo-service): повторная проверка того же груза — штатный сценарий, ради которого
        // существует ручной запуск POST /inspections. От дублей из-за повторной доставки
        // события защищает inbox консьюмера, а не схема.
        builder.HasMany(job => job.Results)
            .WithOne()
            .HasForeignKey(result => result.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
