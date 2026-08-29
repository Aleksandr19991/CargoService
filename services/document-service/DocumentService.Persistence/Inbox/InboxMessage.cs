namespace DocumentService.Persistence.Inbox;

/// <summary>
/// Отметка об обработанном событии (паттерн inbox), тот же приём, что в notification-service.
/// <para>
/// Здесь он защищает от повторного выпуска документов: RabbitMQ доставляет at-least-once, и без
/// отметки вторая доставка <c>CargoAccepted</c> завела бы второй комплект накладной и акта — с
/// новыми идентификаторами, то есть неотличимый от законного повторного выпуска.
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
