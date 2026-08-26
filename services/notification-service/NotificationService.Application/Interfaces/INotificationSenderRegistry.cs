using NotificationService.Domain.Enums;

namespace NotificationService.Application.Interfaces;

public interface INotificationSenderRegistry
{
    /// <summary>
    /// Отправитель для канала или <c>null</c>, если канал в этой сборке не поддержан (сегодня
    /// это Push: шаблонов и провайдера у него нет). Отсутствие отправителя — не ошибка
    /// конфигурации, а нормальное состояние: канал в enum появляется раньше, чем интеграция.
    /// </summary>
    INotificationSender? Resolve(NotificationChannel channel);
}
