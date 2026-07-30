using System.Net;
using System.Net.Http.Json;
using OrdersService.API.Models.Requests;
using OrdersService.API.Models.Responses;
using OrdersService.Domain.Enums;
using OrdersService.Persistence;
using Microsoft.EntityFrameworkCore;

namespace OrdersService.IntegrationTests;

public class OrdersControllerTests(OrdersApiFactory factory) : IClassFixture<OrdersApiFactory>
{
    private static CreateOrderRequest ValidOrderRequest() => new()
    {
        Sender = new OrderPartyRequest { City = "Moscow", OrganizationOrPersonName = "Sender LLC", IsOrganization = true, Phone = "+70001112233" },
        Recipient = new OrderPartyRequest { City = "Kazan", OrganizationOrPersonName = "Recipient LLC", IsOrganization = true, Phone = "+70004445566" },
        OriginCity = "Moscow",
        DestinationCity = "Kazan",
        DistanceKm = 800,
        CargoName = "Electronics",
        CargoWeight = 500,
        CargoVolumeM3 = 2.5m,
        CargoQuantity = 10,
        DeclaredValue = 100000,
        ShippingType = ShippingType.Standard,
        PackagingType = PackagingType.Pallet,
        NeedsPickup = true,
        NeedsDelivery = true,
        NeedsInsurance = false,
    };

    private HttpClient AuthenticatedClient(string role, Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        return client;
    }

    [Fact]
    public async Task CreateOrder_ClientRole_ReturnsCreatedWithGeneratedNumberAndCalculatedPrice()
    {
        var client = AuthenticatedClient("Client", Guid.NewGuid());

        var response = await client.PostAsJsonAsync("api/orders", ValidOrderRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(body);
        Assert.Equal(OrderStatus.Created, body!.Status);
        Assert.Matches(@"^\d{8}-[A-Z0-9]{6}$", body.Number);
        Assert.Equal(FakePricingClient.FixedPrice, body.CalculatedPrice);
    }

    [Fact]
    public async Task CreateOrder_NoAuthHeader_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("api/orders", ValidOrderRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_AdminRole_ReturnsForbidden()
    {
        var client = AuthenticatedClient("Admin", Guid.NewGuid());

        var response = await client.PostAsJsonAsync("api/orders", ValidOrderRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_NegativeDistance_ReturnsBadRequest()
    {
        var client = AuthenticatedClient("Client", Guid.NewGuid());
        var request = ValidOrderRequest() with { DistanceKm = -1 };

        var response = await client.PostAsJsonAsync("api/orders", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_EnqueuesOrderCreatedOutboxMessageInSameTransaction()
    {
        var client = AuthenticatedClient("Client", Guid.NewGuid());

        var createResponse = await client.PostAsJsonAsync("api/orders", ValidOrderRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        await using var context = CreateDbContext();
        var outboxMessage = await context.OutboxMessages.SingleAsync(m => m.RoutingKey == "orders-service.order-created");
        Assert.Contains(created!.Id.ToString(), outboxMessage.Payload);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        var client = AuthenticatedClient("Client", Guid.NewGuid());

        var response = await client.GetAsync($"api/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_OrderBelongsToAnotherClient_ReturnsNotFound()
    {
        var owner = AuthenticatedClient("Client", Guid.NewGuid());
        var createResponse = await owner.PostAsJsonAsync("api/orders", ValidOrderRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        var otherClient = AuthenticatedClient("Client", Guid.NewGuid());
        var response = await otherClient.GetAsync($"api/orders/{created!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_CreatedOrder_ReturnsOkWithConfirmedStatus()
    {
        var client = AuthenticatedClient("Client", Guid.NewGuid());
        var createResponse = await client.PostAsJsonAsync("api/orders", ValidOrderRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        var response = await client.PostAsync($"api/orders/{created!.Id}/confirm", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.Equal(OrderStatus.Confirmed, body!.Status);
    }

    [Fact]
    public async Task Confirm_AlreadyConfirmed_IsIdempotentAndReturnsOk()
    {
        var client = AuthenticatedClient("Client", Guid.NewGuid());
        var createResponse = await client.PostAsJsonAsync("api/orders", ValidOrderRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();
        await client.PostAsync($"api/orders/{created!.Id}/confirm", null);

        var response = await client.PostAsync($"api/orders/{created.Id}/confirm", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_ConfirmedOrder_ReturnsNoContentAndConfirmAfterwardsReturnsConflict()
    {
        var client = AuthenticatedClient("Client", Guid.NewGuid());
        var createResponse = await client.PostAsJsonAsync("api/orders", ValidOrderRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();
        await client.PostAsync($"api/orders/{created!.Id}/confirm", null);

        var cancelResponse = await client.PostAsync($"api/orders/{created.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        var confirmAfterCancel = await client.PostAsync($"api/orders/{created.Id}/confirm", null);
        Assert.Equal(HttpStatusCode.Conflict, confirmAfterCancel.StatusCode);
    }

    [Fact]
    public async Task GetOrders_ReturnsOnlyOwnOrdersWithTotalCount()
    {
        var userId = Guid.NewGuid();
        var client = AuthenticatedClient("Client", userId);
        await client.PostAsJsonAsync("api/orders", ValidOrderRequest());
        await client.PostAsJsonAsync("api/orders", ValidOrderRequest());

        var otherClient = AuthenticatedClient("Client", Guid.NewGuid());
        await otherClient.PostAsJsonAsync("api/orders", ValidOrderRequest());

        var response = await client.GetAsync("api/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrderListResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.TotalCount);
        Assert.Equal(2, body.Items.Count);
    }

    [Fact]
    public async Task GetOrders_StatusFilter_ReturnsOnlyMatchingOrders()
    {
        var userId = Guid.NewGuid();
        var client = AuthenticatedClient("Client", userId);
        var createResponse = await client.PostAsJsonAsync("api/orders", ValidOrderRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();
        await client.PostAsync($"api/orders/{created!.Id}/confirm", null);
        await client.PostAsJsonAsync("api/orders", ValidOrderRequest());

        var response = await client.GetAsync("api/orders?status=Confirmed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrderListResponse>();
        Assert.NotNull(body);
        Assert.Single(body!.Items);
        Assert.Equal(created.Id, body.Items[0].Id);
    }

    [Fact]
    public async Task GetOrders_InvalidPage_ReturnsBadRequest()
    {
        var client = AuthenticatedClient("Client", Guid.NewGuid());

        var response = await client.GetAsync("api/orders?page=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options;

        return new AppDbContext(options);
    }
}
