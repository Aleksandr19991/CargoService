using OrdersService.Domain.Enums;

namespace OrdersService.API.Models.Responses;

public sealed record OrderResponse
{
    public required Guid Id { get; init; }
    public string? Number { get; init; }
    public required OrderStatus Status { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    public required OrderPartyResponse Sender { get; init; }
    public required OrderPartyResponse Recipient { get; init; }

    public required string OriginCity { get; init; }
    public required string DestinationCity { get; init; }
    public required decimal DistanceKm { get; init; }

    public required string CargoName { get; init; }
    public required decimal CargoWeight { get; init; }
    public required decimal CargoVolumeM3 { get; init; }
    public required int CargoQuantity { get; init; }

    public required decimal DeclaredValue { get; init; }

    public DateTimeOffset? RequestedShipDate { get; init; }
    public DateTimeOffset? DeliveryDeadline { get; init; }

    public required OrderServiceOptionsResponse ServiceOptions { get; init; }

    public decimal? CalculatedPrice { get; init; }

    public Guid? SenderCounterpartyId { get; init; }
    public Guid? RecipientCounterpartyId { get; init; }
}
