using NotificationService.Domain.Enums;

namespace NotificationService.Application.Models;

/// <summary>
/// Готовое к отправке сообщение: шаблон уже выбран и плейсхолдеры подставлены. Отправитель
/// канала (<see cref="Interfaces.INotificationSender"/>) не знает ни о событии-поводе, ни о
/// шаблоне — только куда и какой текст доставить.
/// </summary>
public sealed record NotificationMessage
{
    public required NotificationChannel Channel { get; init; }

    /// <summary>Адрес в терминах канала: e-mail или телефон в формате E.164.</summary>
    public required string RecipientContact { get; init; }

    /// <summary>Тема письма; у SMS и Push её нет — отправители этих каналов поле игнорируют.</summary>
    public string? Subject { get; init; }

    public required string Body { get; init; }
}
