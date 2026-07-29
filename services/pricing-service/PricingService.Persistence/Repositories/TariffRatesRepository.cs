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

    public async Task<List<TariffRate>> GetAllCurrentAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        return await context.TariffRates
            .AsNoTracking()
            .Where(rate => rate.ValidFrom <= now && (rate.ValidTo == null || rate.ValidTo > now))
            .OrderBy(rate => rate.Category)
            .ThenBy(rate => rate.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<TariffRate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.TariffRates
            .AsNoTracking()
            .FirstOrDefaultAsync(rate => rate.Id == id, cancellationToken);
    }

    public async Task<TariffRate> ReplaceAsync(TariffRate current, Guid newId, decimal newPrice, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var tracked = await context.TariffRates.FirstAsync(rate => rate.Id == current.Id, cancellationToken);
        tracked.ValidTo = now;

        var replacement = new TariffRate
        {
            Id = newId,
            Category = current.Category,
            Code = current.Code,
            Name = current.Name,
            Price = newPrice,
            PriceType = current.PriceType,
            ValidFrom = now,
            ValidTo = null,
        };
        context.TariffRates.Add(replacement);

        await context.SaveChangesAsync(cancellationToken);
        return replacement;
    }
}
