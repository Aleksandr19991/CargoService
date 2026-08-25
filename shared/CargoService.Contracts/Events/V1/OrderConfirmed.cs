namespace CargoService.Contracts.Events.V1;

/// <summary>Publisher: orders-service. Subscribers: cargo-service, payment-service, notification-service.</summary>
public sealed record OrderConfirmed : IntegrationEvent
{
    public required Guid OrderId { get; init; }
    public required string OrderNumber { get; init; }
    public required decimal CalculatedPrice { get; init; }

    /// <summary>
    /// Желаемый клиентом срок доставки. Едет в событии, чтобы cargo-service мог контролировать SLA
    /// (авто-статус «Задерживается») без синхронного похода в orders-service: значение задаётся
    /// один раз при создании заявки и дальше не меняется — классический event-carried state.
    /// Nullable: клиент вправе срок не указывать, тогда контроль SLA по этому грузу не ведётся.
    /// </summary>
    public DateTimeOffset? DeliveryDeadline { get; init; }
}
