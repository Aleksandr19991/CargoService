using System.Net;
using System.Net.Http.Json;
using PricingService.API.Models.Requests;
using PricingService.API.Models.Responses;
using PricingService.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PricingService.IntegrationTests;

public class TariffsControllerTests(PricingApiFactory factory) : IClassFixture<PricingApiFactory>
{
    private HttpClient AuthenticatedClient(string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }

    [Fact]
    public async Task GetAllTariffs_NoAuth_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("api/tariffs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAllTariffs_ClientRole_ReturnsForbidden()
    {
        var client = AuthenticatedClient("Client");

        var response = await client.GetAsync("api/tariffs");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAllTariffs_ManagerRole_ReturnsSeededTariffs()
    {
        var client = AuthenticatedClient("Manager");

        var response = await client.GetAsync("api/tariffs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<TariffRateResponse>>();
        Assert.NotNull(body);
        Assert.Contains(body!, rate => rate.Category == PricingService.Domain.Enums.TariffCategory.ShippingType && rate.Code == "Standard");
    }

    [Fact]
    public async Task UpdateTariff_ManagerRole_ReturnsForbidden()
    {
        var client = AuthenticatedClient("Manager");

        var response = await client.PutAsJsonAsync(
            $"api/tariffs/{Guid.NewGuid()}", new UpdateTariffRequest { Price = 999m });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTariff_AdminRole_UnknownId_ReturnsNotFound()
    {
        var client = AuthenticatedClient("Admin");

        var response = await client.PutAsJsonAsync(
            $"api/tariffs/{Guid.NewGuid()}", new UpdateTariffRequest { Price = 999m });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTariff_AdminRole_KnownId_VersionsTheRateInDatabase()
    {
        var adminClient = AuthenticatedClient("Admin");
        var managerClient = AuthenticatedClient("Manager");

        var listResponse = await managerClient.GetAsync("api/tariffs");
        var tariffs = await listResponse.Content.ReadFromJsonAsync<List<TariffRateResponse>>();
        var wooden = tariffs!.Single(rate => rate.Code == "Wooden");

        var response = await adminClient.PutAsJsonAsync(
            $"api/tariffs/{wooden.Id}", new UpdateTariffRequest { Price = 350m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<TariffRateResponse>();
        Assert.NotEqual(wooden.Id, updated!.Id);
        Assert.Equal(350m, updated.Price);

        await using var context = CreateDbContext();
        var oldRow = await context.TariffRates.SingleAsync(rate => rate.Id == wooden.Id);
        Assert.NotNull(oldRow.ValidTo);

        var newRow = await context.TariffRates.SingleAsync(rate => rate.Id == updated.Id);
        Assert.Null(newRow.ValidTo);
        Assert.Equal(350m, newRow.Price);
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options;

        return new AppDbContext(options);
    }
}
