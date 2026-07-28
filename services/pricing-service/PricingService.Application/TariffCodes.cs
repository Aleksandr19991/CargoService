namespace PricingService.Application;

/// <summary>
/// Stable TariffRate.Code values, shared between seed data (Persistence's TariffRateConfiguration)
/// and calculation logic (PricingCalculationService) so the two stay in sync — a typo in either
/// place would silently break a lookup instead of failing to compile.
/// </summary>
public static class TariffCodes
{
    public const string BaseRatePerKg = "PerKg";
    public const string BaseRatePerKm = "PerKm";

    public const string ShippingStandard = "Standard";
    public const string ShippingExpress = "Express";

    public const string PackagingWooden = "Wooden";
    public const string PackagingPallet = "Pallet";
    public const string PackagingSpecial = "Special";

    public const string Pickup = "Pickup";
    public const string Delivery = "Delivery";

    public const string InsurancePercentage = "Percentage";
}
