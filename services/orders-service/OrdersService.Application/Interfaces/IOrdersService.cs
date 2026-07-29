using OrdersService.Application.Models;
using OrdersService.Domain.Entities;

namespace OrdersService.Application.Interfaces;

public interface IOrdersService
{
    /// <summary>
    /// Assigns ClientAccountId/Number/Status/CreatedAt, synchronously calls pricing.calculate for
    /// CalculatedPrice, then persists. <paramref name="order"/> should already carry the
    /// caller-supplied fields (Sender/Recipient/cargo/service options/etc.) mapped from the request.
    /// </summary>
    Task<Order> CreateAsync(Guid userId, Order order, CancellationToken cancellationToken = default);

    Task<Order?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<OrderTransitionResult> ConfirmAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<OrderTransitionResult> CancelAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}
