using OrdersService.Domain.Entities;

namespace OrdersService.Application.Interfaces;

public interface IOrdersRepository
{
    Task<Order> CreateAsync(Order order, CancellationToken cancellationToken = default);

    // clientAccountId is always passed explicitly (never trusted from the entity/caller) so
    // ownership is enforced at the query itself, not just checked afterwards.
    Task<Order?> GetByIdAsync(Guid id, Guid clientAccountId, CancellationToken cancellationToken = default);

    Task UpdateAsync(Order order, CancellationToken cancellationToken = default);
}
