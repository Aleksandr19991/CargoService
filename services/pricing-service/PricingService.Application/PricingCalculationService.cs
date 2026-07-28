using PricingService.Application.Interfaces;
using PricingService.Application.Models;
using PricingService.Domain.Entities;
using PricingService.Domain.Enums;

namespace PricingService.Application;

public class PricingCalculationService(ITariffRatesRepository tariffRatesRepository) : IPricingCalculationService
{
    // Standard freight-industry conversion: bulky-but-light shipments are billed by "volumetric
    // weight" (volume * this coefficient) instead of actual weight, whichever is higher. Not a
    // TariffRate — it's a physical conversion factor, not a price — see the earlier decision to
    // keep this a constant rather than a seeded row.
    private const decimal VolumetricWeightCoefficientKgPerM3 = 200m;

    public async Task<PriceCalculationResult> CalculateAsync(PriceCalculationInput input, CancellationToken cancellationToken = default)
    {
        var breakdown = new List<PriceBreakdownLine>();

        var perKg = await GetRateAsync(TariffCategory.BaseRate, TariffCodes.BaseRatePerKg, cancellationToken);
        var chargeableWeight = Math.Max(input.WeightKg, input.VolumeM3 * VolumetricWeightCoefficientKgPerM3);
        breakdown.Add(new PriceBreakdownLine
        {
            Code = perKg.Code,
            Name = "Провозная плата (вес/объём)",
            Amount = chargeableWeight * perKg.Price,
        });

        var perKm = await GetRateAsync(TariffCategory.BaseRate, TariffCodes.BaseRatePerKm, cancellationToken);
        breakdown.Add(new PriceBreakdownLine
        {
            Code = perKm.Code,
            Name = "Провозная плата (расстояние)",
            Amount = input.DistanceKm * perKm.Price,
        });

        var shippingType = await GetRateAsync(TariffCategory.ShippingType, input.ShippingTypeCode, cancellationToken);
        breakdown.Add(new PriceBreakdownLine { Code = shippingType.Code, Name = shippingType.Name, Amount = shippingType.Price });

        var packagingType = await GetRateAsync(TariffCategory.PackagingType, input.PackagingTypeCode, cancellationToken);
        breakdown.Add(new PriceBreakdownLine { Code = packagingType.Code, Name = packagingType.Name, Amount = packagingType.Price });

        if (input.NeedsPickup)
        {
            var pickup = await GetRateAsync(TariffCategory.PickupDelivery, TariffCodes.Pickup, cancellationToken);
            breakdown.Add(new PriceBreakdownLine { Code = pickup.Code, Name = pickup.Name, Amount = pickup.Price });
        }

        if (input.NeedsDelivery)
        {
            var delivery = await GetRateAsync(TariffCategory.PickupDelivery, TariffCodes.Delivery, cancellationToken);
            breakdown.Add(new PriceBreakdownLine { Code = delivery.Code, Name = delivery.Name, Amount = delivery.Price });
        }

        if (input.NeedsInsurance)
        {
            var insurance = await GetRateAsync(TariffCategory.Insurance, TariffCodes.InsurancePercentage, cancellationToken);
            var insuranceAmount = insurance.PriceType == TariffPriceType.Percentage
                ? (input.DeclaredValue ?? 0m) * insurance.Price / 100m
                : insurance.Price;
            breakdown.Add(new PriceBreakdownLine { Code = insurance.Code, Name = insurance.Name, Amount = insuranceAmount });
        }

        return new PriceCalculationResult { TotalPrice = breakdown.Sum(line => line.Amount), Breakdown = breakdown };
    }

    private async Task<TariffRate> GetRateAsync(TariffCategory category, string code, CancellationToken cancellationToken)
    {
        return await tariffRatesRepository.GetCurrentAsync(category, code, cancellationToken)
            ?? throw new InvalidOperationException($"No active tariff rate found for {category}/{code}.");
    }
}
