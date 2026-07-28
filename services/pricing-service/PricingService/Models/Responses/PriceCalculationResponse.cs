namespace PricingService.API.Models.Responses;

public sealed record PriceCalculationResponse
{
    public required decimal TotalPrice { get; init; }
    public required List<PriceBreakdownItemResponse> Breakdown { get; init; }
}

public sealed record PriceBreakdownItemResponse
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required decimal Amount { get; init; }
}
