using NotificationService.Domain.Enums;

namespace NotificationService.Application.Models;

/// <summary>
/// Повод отправить уведомление: какой шаблон взять, кому слать и чем заполнить плейсхолдеры.
/// Собирается обработчиком конкретного события, дальше <c>INotificationsService</c> работает
/// только с этим типом и о событиях больше не знает.
/// </summary>
public sealed record NotificationTrigger
{
    public required string TemplateCode { get; init; }

    /// <summary>
    /// Заявка, по которой определяется получатель (владелец заявки). Null — уведомление
    /// адресовано сотрудникам, а не клиенту (см. <see cref="ToStaff"/>).
    /// </summary>
    public Guid? OrderId { get; init; }

    /// <summary>Слать на общий адрес сотрудников из конфигурации, а не клиенту.</summary>
    public bool ToStaff { get; init; }

    public required IReadOnlyDictionary<string, string?> Placeholders { get; init; }

    public RelatedEntityType? RelatedEntityType { get; init; }
    public Guid? RelatedEntityId { get; init; }
}
