using NotificationService.Application.Models;

namespace NotificationService.Application.Interfaces;

public interface INotificationsService
{
    /// <summary>
    /// Подбирает шаблоны повода, рассылает их по доступным каналам получателя и пишет каждую
    /// попытку в историю. Неизвестный получатель или отсутствие шаблонов — не исключение:
    /// событие уже произошло, откатывать нечего, и повторная доставка того же сообщения ничего
    /// не исправит (см. реализацию).
    /// </summary>
    Task SendAsync(NotificationTrigger trigger, CancellationToken cancellationToken);
}
