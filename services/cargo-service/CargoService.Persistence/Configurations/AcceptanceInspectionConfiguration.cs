using CargoService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CargoService.Persistence.Configurations;

public class AcceptanceInspectionConfiguration : IEntityTypeConfiguration<AcceptanceInspection>
{
    public void Configure(EntityTypeBuilder<AcceptanceInspection> builder)
    {
        builder.ToTable("acceptance_inspections");

        builder.HasKey(inspection => inspection.Id);

        builder.Property(inspection => inspection.ShipmentId)
            .IsRequired();

        builder.Property(inspection => inspection.InspectedByUserId)
            .IsRequired();

        builder.Property(inspection => inspection.PackagingCondition)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(inspection => inspection.CargoCondition)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(inspection => inspection.Comment)
            .HasMaxLength(1000);

        // Npgsql кладёт List<Guid> в нативную колонку uuid[] — ни отдельной таблицы, ни json.
        builder.Property(inspection => inspection.PhotoFileIds)
            .HasColumnType("uuid[]")
            .IsRequired();

        builder.Property(inspection => inspection.InspectedAt)
            .IsRequired();

        builder.HasIndex(inspection => inspection.ShipmentId);
    }
}
