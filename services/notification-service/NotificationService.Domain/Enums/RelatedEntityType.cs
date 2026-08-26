namespace NotificationService.Domain.Enums;

/// <summary>
/// Тип сущности, к которой относится уведомление (поле RelatedEntity из spec.md §2.6, здесь
/// разложенное на тип + идентификатор). Сущности живут в других сервисах, поэтому это именно
/// enum-метка, а не FK: «database per service» кросс-сервисных связей не допускает.
/// </summary>
public enum RelatedEntityType
{
    Order,
    Shipment,
    Payment,
    Document
}
