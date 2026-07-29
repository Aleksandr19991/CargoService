namespace PricingService.Application.Interfaces;

public interface IOutboxWriter
{
    void Enqueue(Guid eventId, string routingKey, string payloadJson, DateTimeOffset occurredAtUtc);
}
