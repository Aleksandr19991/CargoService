using OrdersService.Domain.Enums;

namespace OrdersService.Application.Models;

/// <summary>
/// Mirrors pricing-service's POST /api/pricing/calculate request shape (see
/// PricingService.API.Models.Requests.PriceCalculationRequest) — no shared contract project for
/// HTTP DTOs (CargoService.Contracts is for RabbitMQ events only), so this is a local copy kept in
/// sync by hand. DistanceKm is supplied by the caller — this client has no notion of geography.
/// </summary>
public sealed record PriceCalculationRequest
{
    public required decimal WeightKg { get; init; }
    public required decimal VolumeM3 { get; init; }
    public required decimal DistanceKm { get; init; }
    public required ShippingType ShippingType { get; init; }
    public required PackagingType PackagingType { get; init; }
    public required bool NeedsPickup { get; init; }
    public required bool NeedsDelivery { get; init; }
    public required bool NeedsInsurance { get; init; }
    public decimal? DeclaredValue { get; init; }
}
