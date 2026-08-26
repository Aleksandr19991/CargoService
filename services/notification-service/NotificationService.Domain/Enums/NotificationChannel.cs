namespace NotificationService.Domain.Enums;

/// <summary>
/// Канал доставки уведомления (spec.md §2.6). Telegram в списке каналов ТЗ помечен
/// опциональным и здесь сознательно отсутствует: значение enum, для которого нет ни шаблонов,
/// ни отправителя, только создаёт видимость поддержки — добавится вместе с самим ботом.
/// </summary>
public enum NotificationChannel
{
    Email,
    Sms,
    Push
}
