using NotificationService.Application.Interfaces;
using NotificationService.Domain.Enums;

namespace NotificationService.Application;

/// <summary>
/// Раскладывает зарегистрированных отправителей по каналам. Два отправителя на один канал —
/// ошибка конфигурации, и она обнаруживается при первом обращении, а не тихой победой одного
/// из них.
/// </summary>
public class NotificationSenderRegistry : INotificationSenderRegistry
{
    private readonly Dictionary<NotificationChannel, INotificationSender> senders;

    public NotificationSenderRegistry(IEnumerable<INotificationSender> senders)
    {
        this.senders = senders.ToDictionary(sender => sender.Channel);
    }

    public INotificationSender? Resolve(NotificationChannel channel) =>
        senders.GetValueOrDefault(channel);
}
