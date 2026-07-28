using ClientsService.Application.Interfaces;
using ClientsService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClientsService.Persistence.Repositories;

public class CounterpartiesRepository(AppDbContext context) : ICounterpartiesRepository
{
    public async Task<Counterparty> CreateAsync(Counterparty counterparty, CancellationToken cancellationToken = default)
    {
        context.Counterparties.Add(counterparty);
        await context.SaveChangesAsync(cancellationToken);
        return counterparty;
    }

    public async Task<Counterparty?> GetByIdAsync(Guid id, Guid clientAccountId, CancellationToken cancellationToken = default)
    {
        return await context.Counterparties
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.ClientAccountId == clientAccountId, cancellationToken);
    }

    public async Task<List<Counterparty>> SearchAsync(
        Guid clientAccountId,
        string? city,
        string? name,
        string? phone,
        CancellationToken cancellationToken = default)
    {
        var query = context.Counterparties
            .AsNoTracking()
            .Where(c => c.ClientAccountId == clientAccountId);

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(c => EF.Functions.ILike(c.City, $"%{city}%"));

        if (!string.IsNullOrWhiteSpace(name))
            query = query.Where(c =>
                (c.OrganizationName != null && EF.Functions.ILike(c.OrganizationName, $"%{name}%")) ||
                (c.FullName != null && EF.Functions.ILike(c.FullName, $"%{name}%")));

        if (!string.IsNullOrWhiteSpace(phone))
            query = query.Where(c => EF.Functions.ILike(c.Phone, $"%{phone}%"));

        return await query.OrderBy(c => c.City).ToListAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(Guid id, Guid clientAccountId, Counterparty updated, CancellationToken cancellationToken = default)
    {
        var existing = await context.Counterparties
            .FirstOrDefaultAsync(c => c.Id == id && c.ClientAccountId == clientAccountId, cancellationToken);
        if (existing is null)
            return false;

        existing.Type = updated.Type;
        existing.OrganizationName = updated.OrganizationName;
        existing.FullName = updated.FullName;
        existing.Inn = updated.Inn;
        existing.City = updated.City;
        existing.Phone = updated.Phone;
        existing.Email = updated.Email;

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid clientAccountId, CancellationToken cancellationToken = default)
    {
        var existing = await context.Counterparties
            .FirstOrDefaultAsync(c => c.Id == id && c.ClientAccountId == clientAccountId, cancellationToken);
        if (existing is null)
            return false;

        context.Counterparties.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
