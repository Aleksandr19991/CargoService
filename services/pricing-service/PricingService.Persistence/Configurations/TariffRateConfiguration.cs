using PricingService.Domain.Entities;
using PricingService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PricingService.Persistence.Configurations;

public class TariffRateConfiguration : IEntityTypeConfiguration<TariffRate>
{
    // Fixed instant used as ValidFrom for the seed data below — HasData requires static values,
    // not DateTimeOffset.UtcNow, since it's baked into the migration snapshot at generation time.
    private static readonly DateTimeOffset SeedValidFrom = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<TariffRate> builder)
    {
        builder.ToTable("tariff_rates");

        builder.HasKey(rate => rate.Id);

        builder.Property(rate => rate.Category)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(rate => rate.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(rate => rate.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(rate => rate.Price)
            .HasPrecision(10, 2);

        builder.Property(rate => rate.PriceType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(TariffPriceType.Fixed)
            .IsRequired();

        builder.Property(rate => rate.ValidFrom)
            .IsRequired();

        // Lookup pattern pricing.calculate will use: currently-valid rate for a given
        // category+code. Not unique — a code gets a new row (new ValidFrom, old row's ValidTo
        // closed out) whenever its price changes, so history of past rates is preserved.
        builder.HasIndex(rate => new { rate.Category, rate.Code });

        builder.HasData(
            new TariffRate { Id = Guid.Parse("00000000-0000-0000-0001-000000000001"), Category = TariffCategory.ShippingType, Code = "Standard", Name = "Обычная", Price = 0m, PriceType = TariffPriceType.Fixed, ValidFrom = SeedValidFrom },
            new TariffRate { Id = Guid.Parse("00000000-0000-0000-0001-000000000002"), Category = TariffCategory.ShippingType, Code = "Express", Name = "Экспресс", Price = 500m, PriceType = TariffPriceType.Fixed, ValidFrom = SeedValidFrom },

            new TariffRate { Id = Guid.Parse("00000000-0000-0000-0002-000000000001"), Category = TariffCategory.PackagingType, Code = "Wooden", Name = "Деревянная упаковка", Price = 300m, PriceType = TariffPriceType.Fixed, ValidFrom = SeedValidFrom },
            new TariffRate { Id = Guid.Parse("00000000-0000-0000-0002-000000000002"), Category = TariffCategory.PackagingType, Code = "Pallet", Name = "Паллет", Price = 500m, PriceType = TariffPriceType.Fixed, ValidFrom = SeedValidFrom },
            new TariffRate { Id = Guid.Parse("00000000-0000-0000-0002-000000000003"), Category = TariffCategory.PackagingType, Code = "Special", Name = "Спец. упаковка", Price = 800m, PriceType = TariffPriceType.Fixed, ValidFrom = SeedValidFrom },

            new TariffRate { Id = Guid.Parse("00000000-0000-0000-0003-000000000001"), Category = TariffCategory.PickupDelivery, Code = "Pickup", Name = "Забор", Price = 400m, PriceType = TariffPriceType.Fixed, ValidFrom = SeedValidFrom },
            new TariffRate { Id = Guid.Parse("00000000-0000-0000-0003-000000000002"), Category = TariffCategory.PickupDelivery, Code = "Delivery", Name = "Доставка", Price = 400m, PriceType = TariffPriceType.Fixed, ValidFrom = SeedValidFrom },

            new TariffRate { Id = Guid.Parse("00000000-0000-0000-0004-000000000001"), Category = TariffCategory.Insurance, Code = "Percentage", Name = "Страхование груза", Price = 1.0m, PriceType = TariffPriceType.Percentage, ValidFrom = SeedValidFrom }
        );
    }
}
