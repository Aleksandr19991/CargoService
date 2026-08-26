using CargoService.Contracts.Events.V1;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Models;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.EventHandlers;

public class DocumentGeneratedHandler(INotificationsService notificationsService) : IEventHandler<DocumentGenerated>
{
    public Task HandleAsync(DocumentGenerated @event, CancellationToken cancellationToken) =>
        notificationsService.SendAsync(
            new NotificationTrigger
            {
                TemplateCode = NotificationTemplateCodes.DocumentGenerated,
                OrderId = @event.OrderId,
                Placeholders = new Dictionary<string, string?>
                {
                    ["DocumentType"] = @event.DocumentType,
                },
                // Идентификатор файла в письмо не идёт: ссылку на скачивание выдаёт личный
                // кабинет, а голый GUID клиенту ничего не даёт.
                RelatedEntityType = RelatedEntityType.Document,
                RelatedEntityId = @event.DocumentFileId,
            },
            cancellationToken);
}
