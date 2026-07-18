namespace IdentityService.Application.Interfaces;

/// <summary>
/// Stages an integration event for later publication to RabbitMQ (transactional outbox pattern).
/// The write is only durable once the caller's unit of work is committed — see <see cref="IUsersRepository"/>
/// usages for the "enqueue, then let the next repository call's SaveChanges commit both" convention.
/// </summary>
public interface IOutboxWriter
{
    void Enqueue(Guid eventId, string routingKey, string payloadJson, DateTimeOffset occurredAtUtc);
}
