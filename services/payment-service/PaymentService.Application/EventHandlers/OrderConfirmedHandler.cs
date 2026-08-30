using CargoService.Contracts.Events.V1;
using PaymentService.Application.Interfaces;

namespace PaymentService.Application.EventHandlers;

/// <summary>
/// Подтверждённая заявка — момент, когда платформа вправе просить денег: до подтверждения
/// стоимость ещё может измениться, а после клиенту нужна платёжная ссылка.
/// </summary>
public class OrderConfirmedHandler(IInvoicesService invoices) : IEventHandler<OrderConfirmed>
{
    public Task HandleAsync(OrderConfirmed @event, CancellationToken cancellationToken) =>
        invoices.IssueForConfirmedOrderAsync(
            @event.OrderId,
            @event.OrderNumber,
            @event.CalculatedPrice,
            cancellationToken);
}
