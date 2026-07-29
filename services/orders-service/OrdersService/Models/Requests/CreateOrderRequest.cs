using OrdersService.Domain.Enums;

namespace OrdersService.API.Models.Requests;

public sealed record CreateOrderRequest
{
    public required OrderPartyRequest Sender { get; init; }
    public required OrderPartyRequest Recipient { get; init; }

    public required string OriginCity { get; init; }
    public required string DestinationCity { get; init; }

    // Not in spec.md's original field list — orders-service has no geography/routing data source
    // (future Logistics Service), so the caller supplies distance directly, same as
    // PriceCalculationRequest.DistanceKm on the pricing-service side.
    public required decimal DistanceKm { get; init; }

    public required string CargoName { get; init; }
    public required decimal CargoWeight { get; init; }
    public required decimal CargoVolumeM3 { get; init; }
    public required int CargoQuantity { get; init; }

    public required decimal DeclaredValue { get; init; }

    public DateTimeOffset? RequestedShipDate { get; init; }
    public DateTimeOffset? DeliveryDeadline { get; init; }

    public required ShippingType ShippingType { get; init; }
    public required PackagingType PackagingType { get; init; }
    public required bool NeedsPickup { get; init; }
    public required bool NeedsDelivery { get; init; }
    public required bool NeedsInsurance { get; init; }

    // Optional — the client may type in ad-hoc sender/recipient details instead of picking one
    // of their saved clients-service Counterparty records.
    public Guid? SenderCounterpartyId { get; init; }
    public Guid? RecipientCounterpartyId { get; init; }
}
