using CargoService.Contracts.Events.V1;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Models;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.EventHandlers;

/// <summary>
/// Единственное уведомление не клиенту, а сотрудникам: вердикт модели о повреждении упаковки.
/// <para>
/// Шлётся только когда модель увидела повреждение: событие приходит на каждую проверку, и
/// письмо «повреждений нет» на каждую фотографию превратило бы алерт в фон, который перестают
/// читать. Расхождение с оценкой сотрудника здесь не проверяется — это знание cargo-service (он
/// же его и фиксирует, Фаза 5), а тянуть его сюда значило бы дублировать чужую бизнес-логику
/// вместе с порогом уверенности.
/// </para>
/// </summary>
public class PackageIntegrityAssessedHandler(INotificationsService notificationsService)
    : IEventHandler<PackageIntegrityAssessed>
{
    public Task HandleAsync(PackageIntegrityAssessed @event, CancellationToken cancellationToken)
    {
        if (!@event.DamageDetected)
            return Task.CompletedTask;

        return notificationsService.SendAsync(
            new NotificationTrigger
            {
                TemplateCode = NotificationTemplateCodes.PackageIntegrityAssessed,
                ToStaff = true,
                Placeholders = new Dictionary<string, string?>
                {
                    ["ShipmentId"] = @event.ShipmentId.ToString(),
                    ["Confidence"] = NotificationFormats.Percent(@event.Confidence),
                    ["InspectionJobId"] = @event.InspectionJobId.ToString(),
                },
                RelatedEntityType = RelatedEntityType.Shipment,
                RelatedEntityId = @event.ShipmentId,
            },
            cancellationToken);
    }
}
