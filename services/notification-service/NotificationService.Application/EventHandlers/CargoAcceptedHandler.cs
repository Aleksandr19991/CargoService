using CargoService.Contracts.Events.V1;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Models;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.EventHandlers;

public class CargoAcceptedHandler(INotificationsService notificationsService) : IEventHandler<CargoAccepted>
{
    public Task HandleAsync(CargoAccepted @event, CancellationToken cancellationToken) =>
        notificationsService.SendAsync(
            new NotificationTrigger
            {
                TemplateCode = NotificationTemplateCodes.CargoAccepted,
                OrderId = @event.OrderId,
                Placeholders = new Dictionary<string, string?>
                {
                    ["TrackingNumber"] = @event.TrackingNumber,
                    ["PackagingCondition"] = @event.PackagingCondition,
                    ["CargoCondition"] = @event.CargoCondition,
                },
                // InspectedByUserId в текст не идёт: кто именно принимал груз — внутреннее
                // сведение склада, ровно как в публичном трекинге (Фаза 5).
                RelatedEntityType = RelatedEntityType.Shipment,
                RelatedEntityId = @event.ShipmentId,
            },
            cancellationToken);
}
