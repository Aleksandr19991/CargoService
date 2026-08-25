namespace CargoService.Persistence.Outbox;

/// <summary>
/// A staged integration event awaiting publication to RabbitMQ (transactional outbox pattern).
/// Written in the same DB transaction as the business change it describes; a background
/// dispatcher (<c>CargoService.Infrastructure.Outbox.OutboxDispatcher</c>) publishes pending
/// rows and stamps <see cref="ProcessedAtUtc"/>.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string RoutingKey { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset? ProcessedAtUtc { get; set; }
}
