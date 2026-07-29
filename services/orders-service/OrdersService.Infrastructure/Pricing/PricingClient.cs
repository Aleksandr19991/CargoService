using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrdersService.Application.Interfaces;
using OrdersService.Application.Models;

namespace OrdersService.Infrastructure.Pricing;

/// <summary>
/// Calls pricing-service's public POST /api/pricing/calculate synchronously. Retry + circuit
/// breaker are applied at the HttpClient level (see AddInfrastructure) via Polly, not here — this
/// class only knows about the request/response shape.
/// </summary>
public class PricingClient(HttpClient httpClient) : IPricingClient
{
    // pricing-service serializes enums as strings (JsonStringEnumConverter registered in its own
    // AddApiServices) — the default HttpClient JSON options don't do this, so it must be set
    // explicitly here for both directions.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<PriceCalculationResult> CalculateAsync(PriceCalculationRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/pricing/calculate", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PriceCalculationResult>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("pricing-service returned an empty response for POST /api/pricing/calculate.");
    }
}
