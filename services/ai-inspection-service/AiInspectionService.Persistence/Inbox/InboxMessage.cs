namespace AiInspectionService.Persistence.Inbox;

/// <summary>
/// Отметка об обработанном событии (паттерн inbox), тот же приём, что в notification-service.
/// <para>
/// Здесь он особенно нужен: задания на проверку намеренно допускают повтор по одному грузу
/// (ручной перезапуск, см. `POST /inspections`), поэтому схема дублей не ловит, а повторная
/// доставка <c>CargoPhotoUploaded</c> при at-least-once иначе завела бы второе задание и
/// потратила инференс на те же снимки.
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
