using PricingService.Domain.Enums;

namespace PricingService.Domain.Entities;

public class TariffRate
{
    public Guid Id { get; set; }
    public TariffCategory Category { get; set; }

    // Stable identifier within a category (e.g. "Standard", "Express") — pricing.calculate (see
    // spec.md §2.3) looks up rates by Category+Code rather than by Name, which is display-only.
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }
    public TariffPriceType PriceType { get; set; } = TariffPriceType.Fixed;

    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
}
