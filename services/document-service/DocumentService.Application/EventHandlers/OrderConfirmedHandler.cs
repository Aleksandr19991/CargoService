using CargoService.Contracts.Events.V1;
using DocumentService.Application.Interfaces;

namespace DocumentService.Application.EventHandlers;

/// <summary>
/// Дописывает в сведения о заявке стоимость и срок доставки — их в момент создания заявки ещё
/// нет, а в накладной они печатаются.
/// </summary>
public class OrderConfirmedHandler(IOrderSnapshotsRepository snapshots) : IEventHandler<OrderConfirmed>
{
    public Task HandleAsync(OrderConfirmed @event, CancellationToken cancellationToken) =>
        snapshots.UpdatePriceAsync(
            @event.OrderId,
            @event.CalculatedPrice,
            @event.DeliveryDeadline,
            cancellationToken);
}
