namespace NotificationService.Domain.Entities;

/// <summary>
/// Контакты получателя — локальная read-модель, наполняемая событием <c>UserRegistered</c>.
/// <para>
/// Альтернативой был синхронный запрос контактов в identity-service на каждое статусное
/// событие: лишний сетевой вызов в обработке каждого сообщения и жёсткая зависимость
/// доставки уведомлений от доступности чужого сервиса. Событие уже несёт и адрес, и телефон,
/// поэтому дешевле хранить их у себя (тот же приём, что у clients-service, который на это же
/// событие заводит ClientAccount).
/// </para>
/// </summary>
public class NotificationRecipient
{
    /// <summary>Идентификатор пользователя в identity-service; он же ключ этой таблицы.</summary>
    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}
