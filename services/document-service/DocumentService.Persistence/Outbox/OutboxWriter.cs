using DocumentService.Application.Interfaces;

namespace DocumentService.Persistence.Outbox;

public class OutboxWriter(AppDbContext context) : IOutboxWriter
{
    public void Enqueue(Guid eventId, string routingKey, string payloadJson, DateTimeOffset occurredAtUtc)
    {
        context.OutboxMessages.Add(new OutboxMessage
        {
            Id = eventId,
            RoutingKey = routingKey,
            Payload = payloadJson,
            OccurredAtUtc = occurredAtUtc,
        });
    }
}
