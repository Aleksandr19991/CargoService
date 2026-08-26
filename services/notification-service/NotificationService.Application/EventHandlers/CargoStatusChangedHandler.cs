using CargoService.Contracts.Events.V1;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Models;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.EventHandlers;

public class CargoStatusChangedHandler(INotificationsService notificationsService) : IEventHandler<CargoStatusChanged>
{
    public Task HandleAsync(CargoStatusChanged @event, CancellationToken cancellationToken) =>
        notificationsService.SendAsync(
            new NotificationTrigger
            {
                TemplateCode = NotificationTemplateCodes.CargoStatusChanged,
                OrderId = @event.OrderId,
                Placeholders = new Dictionary<string, string?>
                {
                    ["TrackingNumber"] = @event.TrackingNumber,
                    ["Status"] = @event.Status,
                    ["Location"] = @event.Location,
                },
                RelatedEntityType = RelatedEntityType.Shipment,
                RelatedEntityId = @event.ShipmentId,
            },
            cancellationToken);
}
