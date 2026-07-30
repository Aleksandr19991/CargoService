using OrdersService.Domain.Entities;
using OrdersService.Domain.Enums;

namespace OrdersService.Application.Interfaces;

public interface IOrdersRepository
{
    Task<Order> CreateAsync(Order order, CancellationToken cancellationToken = default);

    // clientAccountId is always passed explicitly (never trusted from the entity/caller) so
    // ownership is enforced at the query itself, not just checked afterwards.
    Task<Order?> GetByIdAsync(Guid id, Guid clientAccountId, CancellationToken cancellationToken = default);

    // No ownership filter — used only by system-level callers (event consumers reacting to
    // CargoStatusChanged/PaymentCompleted) that act on behalf of the whole system, not a specific
    // client's JWT. Never expose this lookup through an HTTP endpoint.
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpdateAsync(Order order, CancellationToken cancellationToken = default);

    // clientAccountId scopes the query to the caller's own orders, same ownership rule as the
    // other client-facing lookups above.
    Task<(List<Order> Items, int TotalCount)> GetByClientAsync(
        Guid clientAccountId,
        OrderStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
