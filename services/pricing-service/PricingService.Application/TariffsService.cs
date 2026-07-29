using System.Text.Json;
using CargoService.Contracts.Events.V1;
using CargoService.Contracts.Messaging;
using PricingService.Application.Interfaces;
using PricingService.Domain.Entities;

namespace PricingService.Application;

public class TariffsService(
    ITariffRatesRepository tariffRatesRepository,
    IOutboxWriter outboxWriter) : ITariffsService
{
    private const string ServiceName = "pricing-service";

    public Task<List<TariffRate>> GetAllCurrentAsync(CancellationToken cancellationToken = default)
    {
        return tariffRatesRepository.GetAllCurrentAsync(cancellationToken);
    }

    public async Task<TariffRate?> UpdatePriceAsync(Guid id, decimal newPrice, CancellationToken cancellationToken = default)
    {
        var current = await tariffRatesRepository.GetByIdAsync(id, cancellationToken);
        if (current is null)
            return null;

        var newId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        EnqueueTariffChangedEvent(newId, current, newPrice);

        // Enqueue() above only stages the outbox row on the same (scoped) DbContext that
        // ReplaceAsync below commits via SaveChangesAsync — this is what makes the new tariff
        // row and the outbox row land in the same database transaction.
        return await tariffRatesRepository.ReplaceAsync(current, newId, newPrice, now, cancellationToken);
    }

    private void EnqueueTariffChangedEvent(Guid newId, TariffRate current, decimal newPrice)
    {
        var integrationEvent = new TariffChanged
        {
            TariffId = newId,
            Category = current.Category.ToString(),
            Code = current.Code,
            Price = newPrice,
        };

        var routingKey = RabbitMqConventions.RoutingKey(ServiceName, nameof(TariffChanged));
        var payloadJson = JsonSerializer.Serialize(integrationEvent);

        outboxWriter.Enqueue(integrationEvent.EventId, routingKey, payloadJson, integrationEvent.OccurredAtUtc);
    }
}
