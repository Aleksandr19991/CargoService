using System.Net;
using System.Net.Http.Json;
using PricingService.API.Models.Requests;
using PricingService.API.Models.Responses;

namespace PricingService.IntegrationTests;

public class PricingControllerTests(PricingApiFactory factory) : IClassFixture<PricingApiFactory>
{
    private static PriceCalculationRequest BaseRequest(
        decimal weightKg = 10,
        decimal volumeM3 = 0.05m,
        decimal distanceKm = 100,
        ShippingTypeOption shippingType = ShippingTypeOption.Standard,
        PackagingTypeOption packagingType = PackagingTypeOption.Wooden,
        bool needsPickup = false,
        bool needsDelivery = false,
        bool needsInsurance = false,
        decimal? declaredValue = null) => new()
        {
            WeightKg = weightKg,
            VolumeM3 = volumeM3,
            DistanceKm = distanceKm,
            ShippingType = shippingType,
            PackagingType = packagingType,
            NeedsPickup = needsPickup,
            NeedsDelivery = needsDelivery,
            NeedsInsurance = needsInsurance,
            DeclaredValue = declaredValue,
        };

    [Fact]
    public async Task Calculate_NoAuthHeader_StillReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("api/pricing/calculate", BaseRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Calculate_ValidRequest_ReturnsBreakdownSummingToTotal()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("api/pricing/calculate", BaseRequest(
            shippingType: ShippingTypeOption.Express,
            needsPickup: true,
            needsDelivery: true,
            needsInsurance: true,
            declaredValue: 10_000m));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PriceCalculationResponse>();
        Assert.NotNull(body);
        Assert.Equal(body!.Breakdown.Sum(line => line.Amount), body.TotalPrice);
        Assert.Contains(body.Breakdown, line => line.Code == "Percentage");
    }

    [Fact]
    public async Task Calculate_ZeroWeightVolumeDistance_ReturnsOnlyPackagingSurcharge()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("api/pricing/calculate", BaseRequest(weightKg: 0, volumeM3: 0, distanceKm: 0));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PriceCalculationResponse>();
        Assert.Equal(300m, body!.TotalPrice); // Wooden packaging only
    }

    [Fact]
    public async Task Calculate_NeedsInsuranceWithoutDeclaredValue_ReturnsBadRequest()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("api/pricing/calculate", BaseRequest(needsInsurance: true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Calculate_NegativeWeight_ReturnsBadRequest()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("api/pricing/calculate", BaseRequest(weightKg: -1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
