using CargoService.Contracts.Events.V1;
using PaymentService.Application.Interfaces;

namespace PaymentService.Application.EventHandlers;

/// <summary>
/// Отменённая заявка закрывает денежную сторону: неоплаченный счёт отменяется, оплаченный —
/// возвращается клиенту.
/// </summary>
public class OrderCancelledHandler(IRefundsService refunds) : IEventHandler<OrderCancelled>
{
    public Task HandleAsync(OrderCancelled @event, CancellationToken cancellationToken) =>
        refunds.RefundForCancelledOrderAsync(
            @event.OrderId,
            @event.OrderNumber,
            @event.Reason,
            cancellationToken);
}
