using ClientsService.Application.Interfaces;
using ClientsService.Domain.Entities;

namespace ClientsService.Application;

public class CounterpartiesService(
    ICounterpartiesRepository counterpartiesRepository,
    IClientAccountsRepository clientAccountsRepository) : ICounterpartiesService
{
    public async Task<Counterparty> CreateAsync(Guid userId, Counterparty counterparty, CancellationToken cancellationToken = default)
    {
        var account = await clientAccountsRepository.GetOrCreateByUserIdAsync(userId, cancellationToken);
        counterparty.ClientAccountId = account.Id;
        return await counterpartiesRepository.CreateAsync(counterparty, cancellationToken);
    }

    public async Task<Counterparty?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var account = await clientAccountsRepository.GetByUserIdAsync(userId, cancellationToken);
        if (account is null)
            return null;

        return await counterpartiesRepository.GetByIdAsync(id, account.Id, cancellationToken);
    }

    public async Task<List<Counterparty>> SearchAsync(
        Guid userId,
        string? city,
        string? name,
        string? phone,
        CancellationToken cancellationToken = default)
    {
        var account = await clientAccountsRepository.GetByUserIdAsync(userId, cancellationToken);
        if (account is null)
            return [];

        return await counterpartiesRepository.SearchAsync(account.Id, city, name, phone, cancellationToken);
    }

    public async Task<bool> UpdateAsync(Guid userId, Guid id, Counterparty counterparty, CancellationToken cancellationToken = default)
    {
        var account = await clientAccountsRepository.GetByUserIdAsync(userId, cancellationToken);
        if (account is null)
            return false;

        return await counterpartiesRepository.UpdateAsync(id, account.Id, counterparty, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var account = await clientAccountsRepository.GetByUserIdAsync(userId, cancellationToken);
        if (account is null)
            return false;

        return await counterpartiesRepository.DeleteAsync(id, account.Id, cancellationToken);
    }
}
