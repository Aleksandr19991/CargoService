using OrdersService.Application.Models;

namespace OrdersService.Application.Interfaces;

public interface IPricingClient
{
    Task<PriceCalculationResult> CalculateAsync(PriceCalculationRequest request, CancellationToken cancellationToken = default);
}
