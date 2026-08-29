using CargoService.Contracts.Events.V1;
using DocumentService.Application.Interfaces;
using DocumentService.Application.Models;
using Microsoft.Extensions.Logging;

namespace DocumentService.Application.EventHandlers;

public class CargoDeliveredHandler(
    IDocumentsService documentsService,
    ILogger<CargoDeliveredHandler> logger) : IEventHandler<CargoDelivered>
{
    public async Task HandleAsync(CargoDelivered @event, CancellationToken cancellationToken)
    {
        var documents = await documentsService.RegisterForDeliveryAsync(
            new CargoDeliveryFacts
            {
                ShipmentId = @event.ShipmentId,
                OrderId = @event.OrderId,
                TrackingNumber = @event.TrackingNumber,
                DeliveredAt = @event.OccurredAtUtc,
                ReceivedByName = @event.ReceivedByName,
            },
            cancellationToken);

        logger.LogInformation(
            "Registered {DocumentCount} document(s) for delivered shipment {TrackingNumber}",
            documents.Count,
            @event.TrackingNumber);
    }
}
