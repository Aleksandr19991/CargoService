using PricingService.Application.Interfaces;
using PricingService.Domain.Entities;
using PricingService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace PricingService.Persistence.Repositories;

public class TariffRatesRepository(AppDbContext context) : ITariffRatesRepository
{
    public async Task<TariffRate?> GetCurrentAsync(TariffCategory category, string code, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        return await context.TariffRates
            .AsNoTracking()
            .Where(rate => rate.Category == category && rate.Code == code)
            .Where(rate => rate.ValidFrom <= now && (rate.ValidTo == null || rate.ValidTo > now))
            .OrderByDescending(rate => rate.ValidFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
