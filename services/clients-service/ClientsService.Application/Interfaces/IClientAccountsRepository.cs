using ClientsService.Domain.Entities;

namespace ClientsService.Application.Interfaces;

public interface IClientAccountsRepository
{
    Task<ClientAccount?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    // Race between two concurrent first-requests for the same user is not handled (would need a
    // unique-violation retry) — acceptable for now, same tradeoff identity-service accepts elsewhere.
    Task<ClientAccount> GetOrCreateByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
