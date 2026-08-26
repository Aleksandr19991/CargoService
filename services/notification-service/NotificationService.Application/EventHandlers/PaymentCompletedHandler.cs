using CargoService.Contracts.Events.V1;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Models;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.EventHandlers;

public class PaymentCompletedHandler(INotificationsService notificationsService) : IEventHandler<PaymentCompleted>
{
    public Task HandleAsync(PaymentCompleted @event, CancellationToken cancellationToken) =>
        notificationsService.SendAsync(
            new NotificationTrigger
            {
                TemplateCode = NotificationTemplateCodes.PaymentCompleted,
                OrderId = @event.OrderId,
                // Номера заявки в событии нет — его подставит сам сервис отправки из привязки
                // заявки к получателю (см. NotificationsService.WithOrderNumber).
                Placeholders = new Dictionary<string, string?>
                {
                    ["Amount"] = NotificationFormats.Money(@event.Amount),
                },
                RelatedEntityType = RelatedEntityType.Payment,
                RelatedEntityId = @event.PaymentId,
            },
            cancellationToken);
}
