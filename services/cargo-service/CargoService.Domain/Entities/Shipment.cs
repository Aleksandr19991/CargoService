using CargoService.Domain.Enums;

namespace CargoService.Domain.Entities;

public class Shipment
{
    public Guid Id { get; set; }

    // References Order.Id in orders-service — no FK/navigation, "database per service" means no
    // cross-service joins; the row is created by the OrderConfirmed consumer (spec.md Phase 5).
    public Guid OrderId { get; set; }

    // Публичный идентификатор для трекинга (GET /track/{trackingNumber}) — генерируется при
    // создании Shipment, уникален в пределах сервиса.
    public string TrackingNumber { get; set; } = string.Empty;

    // Денормализованный «последний статус»: полная хронология лежит в StatusHistory, а это поле
    // избавляет чтение (трекинг, списки) от выборки максимума по истории на каждый запрос.
    public ShipmentStatus CurrentStatus { get; set; } = ShipmentStatus.Created;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Срок доставки из заявки (приезжает в OrderConfirmed). По нему джоба контроля SLA
    /// автоматически переводит просроченные грузы в <see cref="ShipmentStatus.Delayed"/>.
    /// Null — клиент срок не указал, SLA по этому грузу не контролируется.
    /// </summary>
    public DateTimeOffset? DeliveryDeadline { get; set; }

    public ICollection<AcceptanceInspection> Inspections { get; set; } = new List<AcceptanceInspection>();
    public ICollection<PackagingService> PackagingServices { get; set; } = new List<PackagingService>();
    public ICollection<ShipmentStatusHistory> StatusHistory { get; set; } = new List<ShipmentStatusHistory>();
}
