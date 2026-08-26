using CargoService.Contracts.Events.V1;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Models;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.EventHandlers;

public class OrderConfirmedHandler(INotificationsService notificationsService) : IEventHandler<OrderConfirmed>
{
    public Task HandleAsync(OrderConfirmed @event, CancellationToken cancellationToken) =>
        notificationsService.SendAsync(
            new NotificationTrigger
            {
                TemplateCode = NotificationTemplateCodes.OrderConfirmed,
                OrderId = @event.OrderId,
                Placeholders = new Dictionary<string, string?>
                {
                    ["OrderNumber"] = @event.OrderNumber,
                    ["CalculatedPrice"] = NotificationFormats.Money(@event.CalculatedPrice),
                    ["DeliveryDeadline"] = NotificationFormats.Date(@event.DeliveryDeadline),
                },
                RelatedEntityType = RelatedEntityType.Order,
                RelatedEntityId = @event.OrderId,
            },
            cancellationToken);
}
