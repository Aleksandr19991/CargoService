using System.Diagnostics.CodeAnalysis;
using OrdersService.Application.Interfaces;
using OrdersService.Application.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace OrdersService.Infrastructure.Pricing;

/// <summary>
/// IMemoryCache has no built-in "clear everything" — the standard workaround is to have every
/// entry register an expiration token tied to a shared CancellationTokenSource, then Clear()
/// cancels that token (evicting every entry tied to it) and swaps in a fresh one for whatever
/// gets cached next.
/// </summary>
public class InMemoryCalculationCache(IMemoryCache memoryCache) : ICalculationCache
{
    private static readonly TimeSpan EntryLifetime = TimeSpan.FromMinutes(10);

    private CancellationTokenSource _resetTokenSource = new();

    public bool TryGet(PriceCalculationRequest request, [NotNullWhen(true)] out PriceCalculationResult? result)
    {
        return memoryCache.TryGetValue(BuildKey(request), out result);
    }

    public void Set(PriceCalculationRequest request, PriceCalculationResult result)
    {
        var options = new MemoryCacheEntryOptions()
            .AddExpirationToken(new CancellationChangeToken(_resetTokenSource.Token))
            .SetSlidingExpiration(EntryLifetime);

        memoryCache.Set(BuildKey(request), result, options);
    }

    public void Clear()
    {
        var previous = Interlocked.Exchange(ref _resetTokenSource, new CancellationTokenSource());
        previous.Cancel();
        previous.Dispose();
    }

    private static string BuildKey(PriceCalculationRequest request) => string.Join(
        '|',
        request.WeightKg,
        request.VolumeM3,
        request.DistanceKm,
        request.ShippingType,
        request.PackagingType,
        request.NeedsPickup,
        request.NeedsDelivery,
        request.NeedsInsurance,
        request.DeclaredValue);
}
