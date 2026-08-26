using CargoService.Contracts.Events.V1;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Models;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.EventHandlers;

/// <summary>
/// Выдача груза приходит и как <c>CargoStatusChanged</c> со статусом Delivered, и отдельным
/// <c>CargoDelivered</c> (cargo-service публикует оба, Фаза 5) — клиент получит два уведомления
/// об одном событии. Это осознанно оставлено как есть: гасить дубль по смыслу пришлось бы
/// сравнением статуса с текстом «Delivered», то есть завязкой на чужой enum, которого сервис
/// не знает; фильтрация лишнего — дело настроек каналов клиента (задача 6).
/// </summary>
public class CargoDeliveredHandler(INotificationsService notificationsService) : IEventHandler<CargoDelivered>
{
    public Task HandleAsync(CargoDelivered @event, CancellationToken cancellationToken) =>
        notificationsService.SendAsync(
            new NotificationTrigger
            {
                TemplateCode = NotificationTemplateCodes.CargoDelivered,
                OrderId = @event.OrderId,
                Placeholders = new Dictionary<string, string?>
                {
                    ["TrackingNumber"] = @event.TrackingNumber,
                    ["ReceivedByName"] = @event.ReceivedByName,
                },
                RelatedEntityType = RelatedEntityType.Shipment,
                RelatedEntityId = @event.ShipmentId,
            },
            cancellationToken);
}
