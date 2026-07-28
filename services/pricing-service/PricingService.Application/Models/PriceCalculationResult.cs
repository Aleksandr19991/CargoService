namespace PricingService.Application.Models;

public sealed record PriceCalculationResult
{
    public required decimal TotalPrice { get; init; }
    public required List<PriceBreakdownLine> Breakdown { get; init; }
}

public sealed record PriceBreakdownLine
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required decimal Amount { get; init; }
}
