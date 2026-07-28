using PricingService.Domain.Entities;
using PricingService.Domain.Enums;

namespace PricingService.Application.Interfaces;

public interface ITariffRatesRepository
{
    /// <summary>The rate for a category+code that's valid right now (ValidFrom &lt;= now &lt; ValidTo, or ValidTo is null).</summary>
    Task<TariffRate?> GetCurrentAsync(TariffCategory category, string code, CancellationToken cancellationToken = default);
}
