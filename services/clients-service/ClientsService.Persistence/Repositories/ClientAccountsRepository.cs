using ClientsService.Application.Interfaces;
using ClientsService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClientsService.Persistence.Repositories;

public class ClientAccountsRepository(AppDbContext context) : IClientAccountsRepository
{
    public async Task<ClientAccount?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.ClientAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(account => account.UserId == userId, cancellationToken);
    }

    public async Task<ClientAccount> GetOrCreateByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var existing = await context.ClientAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(account => account.UserId == userId, cancellationToken);
        if (existing is not null)
            return existing;

        var account = new ClientAccount { Id = Guid.NewGuid(), UserId = userId };
        context.ClientAccounts.Add(account);
        await context.SaveChangesAsync(cancellationToken);
        return account;
    }
}
