namespace PricingService.Domain.Enums;

/// <summary>
/// Whether TariffRate.Price is a flat currency amount or a percentage — needed for Insurance,
/// which is priced as a percentage of the shipment's declared value rather than a fixed fee.
/// </summary>
public enum TariffPriceType
{
    Fixed,
    Percentage
}
