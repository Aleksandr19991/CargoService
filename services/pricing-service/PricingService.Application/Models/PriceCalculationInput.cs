namespace PricingService.Application.Models;

public sealed record PriceCalculationInput
{
    public required decimal WeightKg { get; init; }
    public required decimal VolumeM3 { get; init; }
    public required decimal DistanceKm { get; init; }

    // TariffRate.Code values (see TariffCodes) — the API layer maps its ShippingType/
    // PackagingType request enums onto these before calling the service.
    public required string ShippingTypeCode { get; init; }
    public required string PackagingTypeCode { get; init; }

    public required bool NeedsPickup { get; init; }
    public required bool NeedsDelivery { get; init; }
    public required bool NeedsInsurance { get; init; }
    public decimal? DeclaredValue { get; init; }
}
