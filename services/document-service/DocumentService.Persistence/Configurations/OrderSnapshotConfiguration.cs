using DocumentService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentService.Persistence.Configurations;

public class OrderSnapshotConfiguration : IEntityTypeConfiguration<OrderSnapshot>
{
    public void Configure(EntityTypeBuilder<OrderSnapshot> builder)
    {
        builder.ToTable("order_snapshots");

        // Ключ — идентификатор заявки из orders-service: своего суррогатного ключа у read-модели
        // нет, а естественный уже уникален и приезжает в каждом событии.
        builder.HasKey(snapshot => snapshot.OrderId);

        builder.Property(snapshot => snapshot.OrderId)
            .ValueGeneratedNever();

        builder.Property(snapshot => snapshot.OrderNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(snapshot => snapshot.OriginCity)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(snapshot => snapshot.DestinationCity)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(snapshot => snapshot.SenderName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(snapshot => snapshot.RecipientName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(snapshot => snapshot.CargoName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(snapshot => snapshot.CargoWeightKg).HasPrecision(10, 3);
        builder.Property(snapshot => snapshot.CargoVolumeM3).HasPrecision(10, 3);
        builder.Property(snapshot => snapshot.DeclaredValue).HasPrecision(12, 2);
        builder.Property(snapshot => snapshot.CalculatedPrice).HasPrecision(12, 2);
    }
}
