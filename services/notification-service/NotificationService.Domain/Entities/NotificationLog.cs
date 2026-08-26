using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

/// <summary>
/// История уведомлений (spec.md §2.6): по записи на каждую попытку отправки, она же источник
/// данных для <c>GET /notifications</c> в личном кабинете клиента.
/// </summary>
public class NotificationLog
{
    public Guid Id { get; set; }

    /// <summary>
    /// Получатель как пользователь identity-service (claim <c>sub</c>) — без FK/навигации, связей
    /// между БД сервисов нет. В списке полей ТЗ этого поля нет, но там же требуется история
    /// «уведомлений клиента»: по одному лишь <see cref="RecipientContact"/> её не отобрать —
    /// адрес меняется, и один адрес может принадлежать разным аккаунтам за время жизни системы.
    /// Null — получатель не пользователь системы (например, алерт на общий ящик склада).
    /// </summary>
    public Guid? RecipientUserId { get; set; }

    /// <summary>Адрес доставки в терминах канала: e-mail, телефон или идентификатор устройства.</summary>
    public string RecipientContact { get; set; } = string.Empty;

    public NotificationChannel Channel { get; set; }

    /// <summary>Код шаблона, по которому собрано сообщение (см. <see cref="NotificationTemplate.Code"/>).</summary>
    public string TemplateCode { get; set; } = string.Empty;

    /// <summary>
    /// Тема и текст в том виде, в каком реально ушли получателю, — снимок, а не ссылка на шаблон:
    /// шаблон правится и живёт дальше, а история должна показывать отправленное, а не то, что
    /// отправилось бы сегодня.
    /// </summary>
    public string? Subject { get; set; }

    public string Body { get; set; } = string.Empty;

    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Момент, когда провайдер принял сообщение. Null, пока статус не <c>Sent</c>.</summary>
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>Причина отказа провайдера. Заполняется только при статусе <c>Failed</c>.</summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Повод уведомления — заявка, груз, платёж или документ (поле RelatedEntity из ТЗ,
    /// разложенное на тип и идентификатор, чтобы по нему можно было искать, а не только
    /// показывать). Оба поля заполняются парой либо оба остаются null.
    /// </summary>
    public RelatedEntityType? RelatedEntityType { get; set; }

    public Guid? RelatedEntityId { get; set; }
}
