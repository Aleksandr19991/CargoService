using CargoService.Contracts.Events.V1;
using DocumentService.Application.Interfaces;
using DocumentService.Application.Models;
using Microsoft.Extensions.Logging;

namespace DocumentService.Application.EventHandlers;

public class CargoAcceptedHandler(
    IDocumentsService documentsService,
    ILogger<CargoAcceptedHandler> logger) : IEventHandler<CargoAccepted>
{
    public async Task HandleAsync(CargoAccepted @event, CancellationToken cancellationToken)
    {
        var documents = await documentsService.RegisterForAcceptanceAsync(
            new CargoAcceptanceFacts
            {
                ShipmentId = @event.ShipmentId,
                OrderId = @event.OrderId,
                TrackingNumber = @event.TrackingNumber,
                // Время приёмки берётся из момента события, а не из часов этого сервиса: в акте
                // должна стоять дата приёмки, а не дата, когда до документа дошли руки.
                AcceptedAt = @event.OccurredAtUtc,
                PackagingCondition = @event.PackagingCondition,
                CargoCondition = @event.CargoCondition,
            },
            cancellationToken);

        logger.LogInformation(
            "Registered {DocumentCount} document(s) for accepted shipment {TrackingNumber}: {Types}",
            documents.Count,
            @event.TrackingNumber,
            string.Join(", ", documents.Select(document => document.Type)));
    }
}
