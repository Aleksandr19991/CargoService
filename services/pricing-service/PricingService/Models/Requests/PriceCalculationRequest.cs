namespace PricingService.API.Models.Requests;

public sealed record PriceCalculationRequest
{
    public required decimal WeightKg { get; init; }
    public required decimal VolumeM3 { get; init; }
    public required decimal DistanceKm { get; init; }
    public required ShippingTypeOption ShippingType { get; init; }
    public required PackagingTypeOption PackagingType { get; init; }
    public required bool NeedsPickup { get; init; }
    public required bool NeedsDelivery { get; init; }
    public required bool NeedsInsurance { get; init; }

    // Required only when NeedsInsurance is true — insurance is priced as a percentage of this.
    public decimal? DeclaredValue { get; init; }
}
