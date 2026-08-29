using CargoService.Contracts.Events.V1;
using DocumentService.Application.Interfaces;
using DocumentService.Domain.Entities;

namespace DocumentService.Application.EventHandlers;

/// <summary>
/// Наполняет read-модель заявки. Документов по этому событию не выпускается: пока груз не
/// принят складом, печатать нечего — но сведения о заявке нужно запомнить заранее, потому что
/// события груза их уже не несут (см. <see cref="OrderSnapshot"/>).
/// </summary>
public class OrderCreatedHandler(IOrderSnapshotsRepository snapshots) : IEventHandler<OrderCreated>
{
    public Task HandleAsync(OrderCreated @event, CancellationToken cancellationToken) =>
        snapshots.UpsertAsync(
            new OrderSnapshot
            {
                OrderId = @event.OrderId,
                OrderNumber = @event.OrderNumber,
                OriginCity = @event.OriginCity,
                DestinationCity = @event.DestinationCity,
                SenderName = @event.SenderName,
                RecipientName = @event.RecipientName,
                CargoName = @event.CargoName,
                CargoWeightKg = @event.CargoWeightKg,
                CargoVolumeM3 = @event.CargoVolumeM3,
                DeclaredValue = @event.DeclaredValue,
            },
            cancellationToken);
}
