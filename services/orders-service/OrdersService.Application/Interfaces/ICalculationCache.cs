using System.Diagnostics.CodeAnalysis;
using OrdersService.Application.Models;

namespace OrdersService.Application.Interfaces;

/// <summary>
/// Memoizes PricingClient.CalculateAsync results by input so repeated calculate calls for the
/// same (weight/volume/distance/services) combination don't hit pricing-service every time (e.g.
/// re-rendering a draft order's price preview). Cleared wholesale whenever a TariffChanged event
/// arrives — see TariffChangedConsumer — since at that point any cached price might be stale and
/// there's no cheap way to tell which entries are actually affected.
/// </summary>
public interface ICalculationCache
{
    bool TryGet(PriceCalculationRequest request, [NotNullWhen(true)] out PriceCalculationResult? result);

    void Set(PriceCalculationRequest request, PriceCalculationResult result);

    void Clear();
}
