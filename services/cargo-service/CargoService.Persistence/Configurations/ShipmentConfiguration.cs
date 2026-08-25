using CargoService.Domain.Entities;
using CargoService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CargoService.Persistence.Configurations;

public class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("shipments");

        builder.HasKey(shipment => shipment.Id);

        builder.Property(shipment => shipment.OrderId)
            .IsRequired();

        builder.Property(shipment => shipment.TrackingNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(shipment => shipment.CurrentStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(ShipmentStatus.Created)
            .IsRequired();

        builder.Property(shipment => shipment.CreatedAt)
            .IsRequired();

        // Джоба контроля SLA выбирает просроченные грузы по паре «срок + текущий статус».
        // Частичный индекс: у грузов без срока SLA не контролируется, в индексе они не нужны.
        builder.HasIndex(shipment => new { shipment.DeliveryDeadline, shipment.CurrentStatus })
            .HasFilter("\"DeliveryDeadline\" IS NOT NULL");

        // Публичный трекинг ищет строго по этому полю, и оно же — идентификатор в глазах клиента.
        builder.HasIndex(shipment => shipment.TrackingNumber)
            .IsUnique();

        // OrderConfirmed приходит из RabbitMQ at-least-once: уникальность по OrderId делает
        // повторную доставку события безобидной вместо создания второго Shipment на ту же заявку.
        builder.HasIndex(shipment => shipment.OrderId)
            .IsUnique();

        builder.HasMany(shipment => shipment.Inspections)
            .WithOne()
            .HasForeignKey(inspection => inspection.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(shipment => shipment.PackagingServices)
            .WithOne()
            .HasForeignKey(packaging => packaging.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(shipment => shipment.StatusHistory)
            .WithOne()
            .HasForeignKey(history => history.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
