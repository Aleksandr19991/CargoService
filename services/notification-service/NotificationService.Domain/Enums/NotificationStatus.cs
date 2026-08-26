namespace NotificationService.Domain.Enums;

/// <summary>Состояние отправки одного уведомления — колонка Status в истории (spec.md §2.6).</summary>
public enum NotificationStatus
{
    /// <summary>Запись создана, провайдер ещё не отвечал.</summary>
    Pending,

    /// <summary>Провайдер принял сообщение (у SMTP/SMS это и есть предел знания сервиса — факт
    /// доставки до почтового ящика/телефона провайдеры подтверждают отдельными webhook'ами,
    /// которых сервис пока не слушает).</summary>
    Sent,

    /// <summary>Отправка не удалась, причина — в FailureReason.</summary>
    Failed
}
