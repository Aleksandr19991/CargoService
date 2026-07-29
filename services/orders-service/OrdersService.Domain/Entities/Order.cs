using OrdersService.Domain.Enums;

namespace OrdersService.Domain.Entities;

public class Order
{
    public Guid Id { get; set; }

    // Assigned when the order leaves Draft (see spec.md Phase 4 "генерация номера заявки") —
    // null for orders that haven't been submitted yet.
    public string? Number { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; }

    public OrderParty Sender { get; set; } = new();
    public OrderParty Recipient { get; set; } = new();

    public string OriginCity { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;

    public string CargoName { get; set; } = string.Empty;
    public decimal CargoWeight { get; set; }
    public int CargoQuantity { get; set; }

    // Cargo's declared value — basis for insurance cost and compensation if lost/damaged.
    public decimal DeclaredValue { get; set; }

    public DateTimeOffset? RequestedShipDate { get; set; }
    public DateTimeOffset? DeliveryDeadline { get; set; }

    public OrderServiceOptions ServiceOptions { get; set; } = new();

    // Populated from pricing-service's POST /pricing/calculate (see spec.md Phase 4).
    public decimal? CalculatedPrice { get; set; }

    // References clients-service data — no FK/navigation, "database per service" means no
    // cross-service joins.
    public Guid ClientAccountId { get; set; }
    public Guid? SenderCounterpartyId { get; set; }
    public Guid? RecipientCounterpartyId { get; set; }
}
