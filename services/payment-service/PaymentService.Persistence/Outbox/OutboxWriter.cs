using PaymentService.Application.Interfaces;

namespace PaymentService.Persistence.Outbox;

public class OutboxWriter(AppDbContext context) : IOutboxWriter
{
    /// <summary>
    /// Только ставит строку в очередь на уровне DbContext, без <c>SaveChanges</c>: сохраняет её
    /// следующий вызов репозитория — оба идут через один и тот же scoped-экземпляр контекста, и
    /// событие коммитится той же транзакцией, что и бизнес-изменение.
    /// </summary>
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
