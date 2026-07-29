namespace PricingService.API.Models.Requests;

public sealed record UpdateTariffRequest
{
    public required decimal Price { get; init; }
}
