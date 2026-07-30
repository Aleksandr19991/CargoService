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

    /// <summary>
    /// Applies a CargoStatusChanged event to the order's read-model fields (TrackingNumber/
    /// CargoStatus). Returns false if no order with this id exists (logged and dropped by the
    /// consumer rather than retried — a missing order is a permanent mismatch, not a transient
    /// failure).
    /// </summary>
    Task<bool> UpdateCargoStatusAsync(Guid orderId, string trackingNumber, string status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a PaymentCompleted event to the order's read-model fields (IsPaid/PaymentId).
    /// Returns false if no order with this id exists.
    /// </summary>
    Task<bool> MarkPaidAsync(Guid orderId, Guid paymentId, CancellationToken cancellationToken = default);
}
