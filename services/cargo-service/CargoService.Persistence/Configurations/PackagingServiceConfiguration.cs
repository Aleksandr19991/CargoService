using CargoService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CargoService.Persistence.Configurations;

public class PackagingServiceConfiguration : IEntityTypeConfiguration<PackagingService>
{
    public void Configure(EntityTypeBuilder<PackagingService> builder)
    {
        builder.ToTable("packaging_services");

        builder.HasKey(packaging => packaging.Id);

        builder.Property(packaging => packaging.ShipmentId)
            .IsRequired();

        builder.Property(packaging => packaging.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(packaging => packaging.PerformedByUserId)
            .IsRequired();

        builder.Property(packaging => packaging.PerformedAt)
            .IsRequired();

        builder.HasIndex(packaging => packaging.ShipmentId);
    }
}
