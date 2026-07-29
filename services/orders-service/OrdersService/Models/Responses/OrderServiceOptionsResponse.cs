using OrdersService.Domain.Enums;

namespace OrdersService.API.Models.Responses;

public sealed record OrderServiceOptionsResponse
{
    public required ShippingType ShippingType { get; init; }
    public required PackagingType PackagingType { get; init; }
    public required bool NeedsPickup { get; init; }
    public required bool NeedsDelivery { get; init; }
    public required bool NeedsInsurance { get; init; }
}
