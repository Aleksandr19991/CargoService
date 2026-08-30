namespace PaymentService.Persistence.Inbox;

/// <summary>
/// Отметка об обработанном событии (паттерн inbox), тот же приём, что в notification-service и
/// document-service.
/// <para>
/// Здесь он защищает от повторного выставления счёта: RabbitMQ доставляет at-least-once, и без
/// отметки вторая доставка <c>OrderConfirmed</c> завела бы у провайдера второй платёж на ту же
/// заявку. Ключ идемпотентности защищает от повтора одного и того же вызова, но не от второго
/// платежа с новым идентификатором — от него защищает эта таблица.
/// </para>
/// <para>
/// Ключ — <c>EventId</c> из <c>IntegrationEvent</c>, а не свойство сообщения <c>MessageId</c>:
/// последнее издатели проставляют по соглашению (и кладут туда идентификатор своей строки
/// outbox), тогда как <c>EventId</c> есть у каждого события по контракту и едет внутри payload.
/// </para>
/// </summary>
public class InboxMessage
{
    public Guid EventId { get; set; }

    /// <summary>Имя типа события — только для разбора инцидентов, в дедупликации не участвует.</summary>
    public string EventType { get; set; } = string.Empty;

    public DateTimeOffset ProcessedAtUtc { get; set; }
}
