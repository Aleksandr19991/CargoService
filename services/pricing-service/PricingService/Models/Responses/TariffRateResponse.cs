using PricingService.Domain.Enums;

namespace PricingService.API.Models.Responses;

public sealed record TariffRateResponse
{
    public required Guid Id { get; init; }
    public required TariffCategory Category { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required decimal Price { get; init; }
    public required TariffPriceType PriceType { get; init; }
    public required DateTimeOffset ValidFrom { get; init; }
    public DateTimeOffset? ValidTo { get; init; }
}
