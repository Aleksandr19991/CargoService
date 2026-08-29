namespace AiInspectionService.Application.Interfaces;

public interface IInboxRepository
{
    Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Отмечает событие обработанным. Возвращает <c>false</c>, если отметка уже была: значит
    /// два экземпляра одного сообщения обрабатывались одновременно и проверка перед обработкой
    /// не успела их развести (уникальный ключ разводит).
    /// </summary>
    Task<bool> TryMarkProcessedAsync(Guid eventId, string eventType, CancellationToken cancellationToken);
}
