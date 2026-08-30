namespace PaymentService.Persistence.Outbox;

/// <summary>
/// A staged integration event awaiting publication to RabbitMQ (transactional outbox pattern).
/// Written in the same DB transaction as the business change it describes; a background
/// dispatcher (<c>PaymentService.Infrastructure.Outbox.OutboxDispatcher</c>) publishes pending
/// rows and stamps <see cref="ProcessedAtUtc"/>.
/// <para>
/// Для денег это не формальность: «списали и не сказали заявке» — расхождение, которое клиент
/// увидит как оплаченную, но не подтверждённую заявку, а прямая публикация в брокер из
/// обработчика webhook именно так и ломалась бы при падении между записью и отправкой.
/// </para>
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string RoutingKey { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset? ProcessedAtUtc { get; set; }
}
