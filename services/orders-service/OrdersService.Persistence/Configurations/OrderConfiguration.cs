using OrdersService.Domain.Entities;
using OrdersService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OrdersService.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(order => order.Id);

        builder.Property(order => order.Number)
            .HasMaxLength(50);

        // Number is always assigned at creation (see Order.Number) — unique so a generation
        // collision surfaces as a clear DbUpdateException rather than silent ambiguity.
        builder.HasIndex(order => order.Number)
            .IsUnique();

        builder.Property(order => order.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(OrderStatus.Draft)
            .IsRequired();

        builder.Property(order => order.CreatedAt)
            .IsRequired();

        builder.Property(order => order.OriginCity)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(order => order.DestinationCity)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(order => order.DistanceKm)
            .HasPrecision(10, 2);

        builder.Property(order => order.CargoName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(order => order.CargoWeight)
            .HasPrecision(10, 2);

        builder.Property(order => order.CargoVolumeM3)
            .HasPrecision(10, 3);

        builder.Property(order => order.DeclaredValue)
            .HasPrecision(12, 2);

        builder.Property(order => order.CalculatedPrice)
            .HasPrecision(12, 2);

        builder.OwnsOne(order => order.Sender, sender =>
        {
            sender.Property(party => party.City).HasColumnName("SenderCity").HasMaxLength(100).IsRequired();
            sender.Property(party => party.OrganizationOrPersonName).HasColumnName("SenderName").HasMaxLength(200).IsRequired();
            sender.Property(party => party.IsOrganization).HasColumnName("SenderIsOrganization").IsRequired();
            sender.Property(party => party.Phone).HasColumnName("SenderPhone").HasMaxLength(20).IsRequired();
        });

        builder.OwnsOne(order => order.Recipient, recipient =>
        {
            recipient.Property(party => party.City).HasColumnName("RecipientCity").HasMaxLength(100).IsRequired();
            recipient.Property(party => party.OrganizationOrPersonName).HasColumnName("RecipientName").HasMaxLength(200).IsRequired();
            recipient.Property(party => party.IsOrganization).HasColumnName("RecipientIsOrganization").IsRequired();
            recipient.Property(party => party.Phone).HasColumnName("RecipientPhone").HasMaxLength(20).IsRequired();
        });

        builder.OwnsOne(order => order.ServiceOptions, options =>
        {
            options.Property(o => o.ShippingType).HasColumnName("ShippingType").HasConversion<string>().HasMaxLength(20).IsRequired();
            options.Property(o => o.PackagingType).HasColumnName("PackagingType").HasConversion<string>().HasMaxLength(20).IsRequired();
            options.Property(o => o.NeedsPickup).HasColumnName("NeedsPickup").IsRequired();
            options.Property(o => o.NeedsDelivery).HasColumnName("NeedsDelivery").IsRequired();
            options.Property(o => o.NeedsInsurance).HasColumnName("NeedsInsurance").IsRequired();
        });

        builder.Property(order => order.ClientAccountId)
            .IsRequired();

        builder.HasIndex(order => order.ClientAccountId);
    }
}
