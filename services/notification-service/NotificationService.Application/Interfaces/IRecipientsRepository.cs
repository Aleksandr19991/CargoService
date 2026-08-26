using NotificationService.Domain.Entities;

namespace NotificationService.Application.Interfaces;

/// <summary>
/// Read-модель «куда слать»: контакты пользователей и привязка заявки к её владельцу. Две
/// таблицы под одним репозиторием сознательно — они существуют ради одного и того же вопроса,
/// и любой ответ на него читает обе (заявка → владелец → контакты).
/// </summary>
public interface IRecipientsRepository
{
    Task<NotificationRecipient?> GetRecipientAsync(Guid userId, CancellationToken cancellationToken);

    Task<OrderRecipient?> GetOrderRecipientAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Создаёт или обновляет контакты пользователя (событие может прийти повторно).</summary>
    Task UpsertRecipientAsync(NotificationRecipient recipient, CancellationToken cancellationToken);

    /// <summary>Создаёт или обновляет привязку заявки к владельцу.</summary>
    Task UpsertOrderRecipientAsync(OrderRecipient orderRecipient, CancellationToken cancellationToken);
}
