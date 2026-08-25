using CargoService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CargoService.Persistence.Configurations;

public class ShipmentStatusHistoryConfiguration : IEntityTypeConfiguration<ShipmentStatusHistory>
{
    public void Configure(EntityTypeBuilder<ShipmentStatusHistory> builder)
    {
        builder.ToTable("shipment_status_history");

        builder.HasKey(history => history.Id);

        builder.Property(history => history.ShipmentId)
            .IsRequired();

        builder.Property(history => history.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(history => history.ChangedAt)
            .IsRequired();

        builder.Property(history => history.Location)
            .HasMaxLength(100);

        builder.Property(history => history.Comment)
            .HasMaxLength(1000);

        // Трекинг всегда читает историю одного груза в хронологическом порядке.
        builder.HasIndex(history => new { history.ShipmentId, history.ChangedAt });
    }
}
