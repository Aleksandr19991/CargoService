namespace CargoService.Contracts.Events.V1;

/// <summary>
/// Publisher: orders-service. Subscribers: notification-service, document-service, reporting-service.
/// </summary>
public sealed record OrderCreated : IntegrationEvent
{
    public required Guid OrderId { get; init; }
    public required string OrderNumber { get; init; }
    public required Guid ClientAccountId { get; init; }
    public required string OriginCity { get; init; }
    public required string DestinationCity { get; init; }

    // Описание груза и сторон добавлено ради document-service (Фаза 8): транспортная накладная
    // без отправителя, получателя и характеристик груза — не документ. Значения задаются при
    // создании заявки и дальше не меняются, то есть это тот же event-carried state, что и
    // DeliveryDeadline в OrderConfirmed; альтернативой был бы синхронный вызов в orders-service
    // на каждую печать.
    public required string SenderName { get; init; }
    public required string RecipientName { get; init; }
    public required string CargoName { get; init; }
    public required decimal CargoWeightKg { get; init; }
    public required decimal CargoVolumeM3 { get; init; }
    public required decimal DeclaredValue { get; init; }
}
