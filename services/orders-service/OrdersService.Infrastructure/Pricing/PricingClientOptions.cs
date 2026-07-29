namespace OrdersService.Infrastructure.Pricing;

public class PricingClientOptions
{
    public const string SectionName = "PricingService";

    public required string BaseUrl { get; init; }
}
