using ClientsService.Domain.Entities;

namespace ClientsService.Application.Interfaces;

public interface ICounterpartiesRepository
{
    Task<Counterparty> CreateAsync(Counterparty counterparty, CancellationToken cancellationToken = default);

    // clientAccountId is always passed explicitly (never trusted from the entity/caller) so
    // ownership is enforced at the query itself, not just checked afterwards.
    Task<Counterparty?> GetByIdAsync(Guid id, Guid clientAccountId, CancellationToken cancellationToken = default);

    Task<List<Counterparty>> SearchAsync(
        Guid clientAccountId,
        string? city,
        string? name,
        string? phone,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(Guid id, Guid clientAccountId, Counterparty updated, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, Guid clientAccountId, CancellationToken cancellationToken = default);
}
