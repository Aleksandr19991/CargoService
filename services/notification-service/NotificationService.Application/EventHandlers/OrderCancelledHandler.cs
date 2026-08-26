using CargoService.Contracts.Events.V1;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Models;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.EventHandlers;

public class OrderCancelledHandler(INotificationsService notificationsService) : IEventHandler<OrderCancelled>
{
    public Task HandleAsync(OrderCancelled @event, CancellationToken cancellationToken) =>
        notificationsService.SendAsync(
            new NotificationTrigger
            {
                TemplateCode = NotificationTemplateCodes.OrderCancelled,
                OrderId = @event.OrderId,
                Placeholders = new Dictionary<string, string?>
                {
                    ["OrderNumber"] = @event.OrderNumber,
                    // orders-service причину отмены не собирает (Фаза 4), поэтому поле обычно
                    // пустое и в письме превращается в прочерк.
                    ["Reason"] = @event.Reason,
                },
                RelatedEntityType = RelatedEntityType.Order,
                RelatedEntityId = @event.OrderId,
            },
            cancellationToken);
}
