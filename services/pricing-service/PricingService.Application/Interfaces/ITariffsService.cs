using PricingService.Domain.Entities;

namespace PricingService.Application.Interfaces;

public interface ITariffsService
{
    Task<List<TariffRate>> GetAllCurrentAsync(CancellationToken cancellationToken = default);

    /// <returns>The new (replacement) rate, or null if <paramref name="id"/> doesn't match an existing rate.</returns>
    Task<TariffRate?> UpdatePriceAsync(Guid id, decimal newPrice, CancellationToken cancellationToken = default);
}
