using CargoService.Contracts.Events.V1;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Models;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.EventHandlers;

/// <summary>
/// Первое событие цепочки заявки: помимо самого уведомления запоминает, кому эта заявка
/// принадлежит — все последующие события несут только <c>OrderId</c>
/// (см. <see cref="OrderRecipient"/>).
/// </summary>
public class OrderCreatedHandler(
    IRecipientsRepository recipientsRepository,
    INotificationsService notificationsService) : IEventHandler<OrderCreated>
{
    public async Task HandleAsync(OrderCreated @event, CancellationToken cancellationToken)
    {
        // Привязка сохраняется до отправки: без неё получателя не найти даже для этого письма.
        // ClientAccountId в orders-service — это идентификатор пользователя из токена (Фаза 4),
        // то есть тот же ключ, что и у контактов из UserRegistered.
        await recipientsRepository.UpsertOrderRecipientAsync(
            new OrderRecipient
            {
                OrderId = @event.OrderId,
                RecipientUserId = @event.ClientAccountId,
                OrderNumber = @event.OrderNumber,
            },
            cancellationToken);

        await notificationsService.SendAsync(
            new NotificationTrigger
            {
                TemplateCode = NotificationTemplateCodes.OrderCreated,
                OrderId = @event.OrderId,
                Placeholders = new Dictionary<string, string?>
                {
                    ["OrderNumber"] = @event.OrderNumber,
                    ["OriginCity"] = @event.OriginCity,
                    ["DestinationCity"] = @event.DestinationCity,
                },
                RelatedEntityType = RelatedEntityType.Order,
                RelatedEntityId = @event.OrderId,
            },
            cancellationToken);
    }
}
