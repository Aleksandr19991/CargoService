namespace NotificationService.Domain.Entities;

/// <summary>
/// Кому слать уведомления по конкретной заявке — read-модель, наполняемая событием
/// <c>OrderCreated</c>.
/// <para>
/// Нужна потому, что получателя называет только самое первое событие цепочки: в
/// <c>OrderCreated</c> есть <c>ClientAccountId</c> (в orders-service это и есть идентификатор
/// пользователя из токена), а все последующие события — подтверждение, приёмка, смена статуса,
/// оплата, документы — несут только <c>OrderId</c>. Без этой связки уведомление по ним было бы
/// некуда отправить.
/// </para>
/// </summary>
public class OrderRecipient
{
    public Guid OrderId { get; set; }

    /// <summary>Владелец заявки — ключ в <see cref="NotificationRecipient"/>.</summary>
    public Guid RecipientUserId { get; set; }

    /// <summary>Человекочитаемый номер заявки: им заявка называется в текстах уведомлений.</summary>
    public string OrderNumber { get; set; } = string.Empty;
}
