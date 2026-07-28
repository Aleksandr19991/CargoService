using ClientsService.Domain.Entities;

namespace ClientsService.Application.Interfaces;

public interface ICounterpartiesService
{
    Task<Counterparty> CreateAsync(Guid userId, Counterparty counterparty, CancellationToken cancellationToken = default);

    Task<Counterparty?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<List<Counterparty>> SearchAsync(
        Guid userId,
        string? city,
        string? name,
        string? phone,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(Guid userId, Guid id, Counterparty counterparty, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}
