using CargoService.Contracts.Events.V1;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Models;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.EventHandlers;

public class PaymentFailedHandler(INotificationsService notificationsService) : IEventHandler<PaymentFailed>
{
    public Task HandleAsync(PaymentFailed @event, CancellationToken cancellationToken) =>
        notificationsService.SendAsync(
            new NotificationTrigger
            {
                TemplateCode = NotificationTemplateCodes.PaymentFailed,
                OrderId = @event.OrderId,
                Placeholders = new Dictionary<string, string?>
                {
                    ["Reason"] = @event.Reason,
                },
                RelatedEntityType = RelatedEntityType.Payment,
                RelatedEntityId = @event.PaymentId,
            },
            cancellationToken);
}
