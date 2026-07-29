using PricingService.Domain.Entities;
using PricingService.Domain.Enums;

namespace PricingService.Application.Interfaces;

public interface ITariffRatesRepository
{
    /// <summary>The rate for a category+code that's valid right now (ValidFrom &lt;= now &lt; ValidTo, or ValidTo is null).</summary>
    Task<TariffRate?> GetCurrentAsync(TariffCategory category, string code, CancellationToken cancellationToken = default);

    /// <summary>Every rate that's valid right now, across all categories/codes — for GET /tariffs.</summary>
    Task<List<TariffRate>> GetAllCurrentAsync(CancellationToken cancellationToken = default);

    /// <summary>The rate by its own Id (not "current for category+code") — PUT /tariffs/{id} operates on a specific row.</summary>
    Task<TariffRate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes <paramref name="current"/> (ValidTo = <paramref name="now"/>) and inserts a new row
    /// with the same Category/Code/Name/PriceType but <paramref name="newPrice"/>, ValidFrom =
    /// <paramref name="now"/> — a single SaveChanges, so both changes commit atomically together
    /// with whatever the caller already staged on the same DbContext (e.g. an outbox message).
    /// </summary>
    Task<TariffRate> ReplaceAsync(TariffRate current, Guid newId, decimal newPrice, DateTimeOffset now, CancellationToken cancellationToken = default);
}
