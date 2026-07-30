using OrdersService.Application.Interfaces;
using OrdersService.Application.Models;

namespace OrdersService.IntegrationTests;

/// <summary>
/// Stands in for pricing-service in integration tests — no real pricing-service (or the RabbitMQ
/// it'd need for cache invalidation) is available. Returns a fixed price regardless of input,
/// enough for tests that only assert the order gets *some* CalculatedPrice.
/// </summary>
public class FakePricingClient : IPricingClient
{
    public const decimal FixedPrice = 12345.67m;

    public Task<PriceCalculationResult> CalculateAsync(PriceCalculationRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PriceCalculationResult { TotalPrice = FixedPrice, Breakdown = [] });
    }
}
