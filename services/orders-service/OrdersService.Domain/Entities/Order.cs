using OrdersService.Domain.Enums;

namespace OrdersService.Domain.Entities;

public class Order
{
    public Guid Id { get; set; }

    // Generated at creation time (POST /orders always creates directly in Created status — there's
    // no separate "save draft" endpoint) — nullable only because Draft remains the entity's
    // default/unused status for now.
    public string? Number { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; }

    public OrderParty Sender { get; set; } = new();
    public OrderParty Recipient { get; set; } = new();

    public string OriginCity { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;

    // Not in spec.md's original field list — added because pricing.calculate requires a numeric
    // distance and orders-service has no geography/routing data source (that's the future
    // Logistics Service). The client supplies it directly, same as PriceCalculationRequest.
    public decimal DistanceKm { get; set; }

    public string CargoName { get; set; } = string.Empty;
    public decimal CargoWeight { get; set; }
    public int CargoQuantity { get; set; }

    // Not in spec.md's original field list — same reasoning as DistanceKm: pricing.calculate needs
    // volume too (chargeable/volumetric weight), and there's nowhere else to source it from.
    public decimal CargoVolumeM3 { get; set; }

    // Cargo's declared value — basis for insurance cost and compensation if lost/damaged.
    public decimal DeclaredValue { get; set; }

    public DateTimeOffset? RequestedShipDate { get; set; }
    public DateTimeOffset? DeliveryDeadline { get; set; }

    public OrderServiceOptions ServiceOptions { get; set; } = new();

    // Populated from pricing-service's POST /pricing/calculate (see spec.md Phase 4).
    public decimal? CalculatedPrice { get; set; }

    // Actually holds the Identity User.Id (JWT `sub` claim) of the client who created the order,
    // not clients-service's ClientAccount.Id — resolving the real ClientAccount.Id would need a
    // new synchronous call to clients-service, which we decided against for now. Named
    // ClientAccountId to match spec.md's field list; ownership checks and the future personal
    // cabinet (GET /orders?clientId=) key off this same UserId.
    public Guid ClientAccountId { get; set; }

    // References clients-service data — no FK/navigation, "database per service" means no
    // cross-service joins.
    public Guid? SenderCounterpartyId { get; set; }
    public Guid? RecipientCounterpartyId { get; set; }

    // Not in spec.md's original field list — a read-model projection of cargo-service's Shipment,
    // kept in sync by CargoStatusChangedConsumer (see spec.md Phase 4). Null until the order's
    // shipment is created and the first status event arrives.
    public string? TrackingNumber { get; set; }
    public string? CargoStatus { get; set; }

    // Not in spec.md's original field list — a read-model projection of payment-service's payment
    // record, kept in sync by PaymentCompletedConsumer (see spec.md Phase 4).
    public bool IsPaid { get; set; }
    public Guid? PaymentId { get; set; }
}
