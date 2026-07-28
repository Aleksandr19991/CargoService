using PricingService.Application.Models;

namespace PricingService.Application.Interfaces;

public interface IPricingCalculationService
{
    Task<PriceCalculationResult> CalculateAsync(PriceCalculationInput input, CancellationToken cancellationToken = default);
}
