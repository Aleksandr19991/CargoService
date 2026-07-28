using System.Net;
using System.Net.Http.Json;
using ClientsService.API.Models.Requests;
using ClientsService.API.Models.Responses;
using ClientsService.Domain.Enums;
using ClientsService.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClientsService.IntegrationTests;

public class CounterpartiesControllerTests(ClientsApiFactory factory) : IClassFixture<ClientsApiFactory>
{
    private static CreateCounterpartyRequest OrganizationRequest(string city = "Москва") => new()
    {
        Type = CounterpartyType.Organization,
        OrganizationName = "ООО Ромашка",
        City = city,
        Phone = "+79991234567",
        Email = "org@example.com",
        Inn = "7701234567",
    };

    private HttpClient AuthenticatedClient(string role, Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        return client;
    }

    [Fact]
    public async Task Create_ClientRole_PersistsCounterpartyAndCreatesClientAccount()
    {
        var userId = Guid.NewGuid();
        var client = AuthenticatedClient("Client", userId);

        var response = await client.PostAsJsonAsync("api/counterparties", OrganizationRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CounterpartyResponse>();
        Assert.NotNull(body);
        Assert.Equal(CounterpartyType.Organization, body!.Type);

        await using var context = CreateDbContext();
        var account = await context.ClientAccounts.SingleAsync(a => a.UserId == userId);
        var stored = await context.Counterparties.SingleAsync(c => c.Id == body.Id);
        Assert.Equal(account.Id, stored.ClientAccountId);
    }

    [Fact]
    public async Task Create_NoAuthHeader_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("api/counterparties", OrganizationRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_NonClientRole_ReturnsForbidden()
    {
        var client = AuthenticatedClient("Admin", Guid.NewGuid());

        var response = await client.PostAsJsonAsync("api/counterparties", OrganizationRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_OrganizationWithoutOrganizationName_ReturnsBadRequest()
    {
        var client = AuthenticatedClient("Client", Guid.NewGuid());

        var response = await client.PostAsJsonAsync("api/counterparties", new CreateCounterpartyRequest
        {
            Type = CounterpartyType.Organization,
            City = "Москва",
            Phone = "+79991234567",
            Email = "org@example.com",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        var client = AuthenticatedClient("Client", Guid.NewGuid());

        var response = await client.GetAsync($"api/counterparties/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_CounterpartyBelongsToAnotherUser_ReturnsNotFound()
    {
        var ownerClient = AuthenticatedClient("Client", Guid.NewGuid());
        var createResponse = await ownerClient.PostAsJsonAsync("api/counterparties", OrganizationRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CounterpartyResponse>();

        var otherClient = AuthenticatedClient("Client", Guid.NewGuid());
        var response = await otherClient.GetAsync($"api/counterparties/{created!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Search_ByCity_ReturnsOnlyMatchingCounterparties()
    {
        var userId = Guid.NewGuid();
        var client = AuthenticatedClient("Client", userId);
        var uniqueCity = $"Город-{Guid.NewGuid():N}";

        await client.PostAsJsonAsync("api/counterparties", OrganizationRequest(uniqueCity));
        await client.PostAsJsonAsync("api/counterparties", OrganizationRequest("Другой город"));

        var response = await client.GetAsync($"api/counterparties?city={Uri.EscapeDataString(uniqueCity)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<List<CounterpartyResponse>>();
        Assert.NotNull(results);
        Assert.Single(results!);
        Assert.Equal(uniqueCity, results![0].City);
    }

    [Fact]
    public async Task Update_ExistingCounterparty_UpdatesFieldsInDatabase()
    {
        var client = AuthenticatedClient("Client", Guid.NewGuid());
        var createResponse = await client.PostAsJsonAsync("api/counterparties", OrganizationRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CounterpartyResponse>();

        var response = await client.PutAsJsonAsync($"api/counterparties/{created!.Id}", new UpdateCounterpartyRequest
        {
            Type = CounterpartyType.Organization,
            OrganizationName = "ООО Ромашка",
            City = "Новосибирск",
            Phone = "+70000000000",
            Email = "org@example.com",
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var context = CreateDbContext();
        var stored = await context.Counterparties.SingleAsync(c => c.Id == created.Id);
        Assert.Equal("Новосибирск", stored.City);
        Assert.Equal("+70000000000", stored.Phone);
    }

    [Fact]
    public async Task Update_CounterpartyBelongsToAnotherUser_ReturnsNotFound()
    {
        var ownerClient = AuthenticatedClient("Client", Guid.NewGuid());
        var createResponse = await ownerClient.PostAsJsonAsync("api/counterparties", OrganizationRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CounterpartyResponse>();

        var otherClient = AuthenticatedClient("Client", Guid.NewGuid());
        var response = await otherClient.PutAsJsonAsync($"api/counterparties/{created!.Id}", new UpdateCounterpartyRequest
        {
            Type = CounterpartyType.Organization,
            OrganizationName = "Чужая организация",
            City = "Москва",
            Phone = "+70000000000",
            Email = "other@example.com",
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingCounterparty_RemovesItAndSubsequentGetReturnsNotFound()
    {
        var client = AuthenticatedClient("Client", Guid.NewGuid());
        var createResponse = await client.PostAsJsonAsync("api/counterparties", OrganizationRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CounterpartyResponse>();

        var deleteResponse = await client.DeleteAsync($"api/counterparties/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"api/counterparties/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options;

        return new AppDbContext(options);
    }
}
