using System.Net;
using System.Net.Http.Json;
using CargoService.API.Models.Requests;
using CargoService.API.Models.Responses;
using CargoService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CargoService.IntegrationTests;

public class ShipmentsControllerTests(CargoApiFactory factory) : IClassFixture<CargoApiFactory>
{
    private static AcceptShipmentRequest ValidAcceptRequest() => new()
    {
        PackagingCondition = PackagingCondition.Damaged,
        CargoCondition = CargoCondition.Intact,
        Comment = "Угол смят",
        PhotoFileIds = [Guid.NewGuid()],
        PerformedPackagingTypes = [PackagingType.Pallet],
        Location = "Москва, склад №1",
    };

    private HttpClient AuthenticatedClient(string role, Guid? userId = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, (userId ?? Guid.NewGuid()).ToString());
        return client;
    }

    [Fact]
    public async Task GetById_NoAuthHeader_ReturnsUnauthorized()
    {
        var shipment = await factory.SeedShipmentAsync();

        var response = await factory.CreateClient().GetAsync($"api/shipments/{shipment.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ClientRole_ReturnsForbidden()
    {
        var shipment = await factory.SeedShipmentAsync();

        var response = await AuthenticatedClient("Client").GetAsync($"api/shipments/{shipment.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        var response = await AuthenticatedClient("WarehouseOperator").GetAsync($"api/shipments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Accept_RecordsActPackagingAndStatusFromTokenIdentity()
    {
        var shipment = await factory.SeedShipmentAsync();
        var operatorId = Guid.NewGuid();
        var client = AuthenticatedClient("WarehouseOperator", operatorId);

        var response = await client.PostAsJsonAsync($"api/shipments/{shipment.Id}/accept", ValidAcceptRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ShipmentResponse>();
        Assert.Equal(ShipmentStatus.Accepted, body!.CurrentStatus);

        var inspection = Assert.Single(body.Inspections);
        // Личность приёмщика берётся из токена, а не из тела запроса.
        Assert.Equal(operatorId, inspection.InspectedByUserId);
        Assert.Equal(PackagingCondition.Damaged, inspection.PackagingCondition);
        Assert.Single(body.PackagingServices);

        // Событие и бизнес-изменение коммитятся одной транзакцией.
        await using var context = factory.CreateDbContext();
        var routingKeys = await context.OutboxMessages
            .Where(message => message.Payload.Contains(shipment.Id.ToString()))
            .Select(message => message.RoutingKey)
            .ToListAsync();
        Assert.Contains("cargo-service.cargo-accepted", routingKeys);
        Assert.Contains("cargo-service.cargo-status-changed", routingKeys);
    }

    [Fact]
    public async Task Accept_CourierRole_ReturnsForbidden()
    {
        var shipment = await factory.SeedShipmentAsync();

        var response = await AuthenticatedClient("Courier")
            .PostAsJsonAsync($"api/shipments/{shipment.Id}/accept", ValidAcceptRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Accept_AlreadyAccepted_ReturnsConflict()
    {
        var shipment = await factory.SeedShipmentAsync(ShipmentStatus.Accepted);

        var response = await AuthenticatedClient("WarehouseOperator")
            .PostAsJsonAsync($"api/shipments/{shipment.Id}/accept", ValidAcceptRequest());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AddPhotos_BeforeAcceptance_ReturnsConflict()
    {
        var shipment = await factory.SeedShipmentAsync();

        var response = await AuthenticatedClient("WarehouseOperator")
            .PostAsJsonAsync($"api/shipments/{shipment.Id}/photos", new AddShipmentPhotosRequest { PhotoFileIds = [Guid.NewGuid()] });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AddPhotos_EmptyList_ReturnsBadRequest()
    {
        var shipment = await factory.SeedShipmentAsync();

        var response = await AuthenticatedClient("WarehouseOperator")
            .PostAsJsonAsync($"api/shipments/{shipment.Id}/photos", new AddShipmentPhotosRequest { PhotoFileIds = [] });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddPhotos_DeduplicatesAgainstExistingFiles()
    {
        var shipment = await factory.SeedShipmentAsync();
        var client = AuthenticatedClient("WarehouseOperator");
        var request = ValidAcceptRequest();
        var acceptResponse = await client.PostAsJsonAsync($"api/shipments/{shipment.Id}/accept", request);
        var accepted = await acceptResponse.Content.ReadFromJsonAsync<ShipmentResponse>();
        var alreadyThere = accepted!.Inspections.Single().PhotoFileIds.Single();
        var fresh = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(
            $"api/shipments/{shipment.Id}/photos",
            new AddShipmentPhotosRequest { PhotoFileIds = [alreadyThere, fresh] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ShipmentResponse>();
        Assert.Equal([alreadyThere, fresh], body!.Inspections.Single().PhotoFileIds);
    }

    [Fact]
    public async Task ChangeStatus_CourierIsAllowedAndHistoryGrows()
    {
        var shipment = await factory.SeedShipmentAsync(ShipmentStatus.Accepted);
        var client = AuthenticatedClient("Courier");

        await client.PostAsJsonAsync($"api/shipments/{shipment.Id}/status",
            new ChangeShipmentStatusRequest { Status = ShipmentStatus.InTransit, Location = "Москва" });
        var response = await client.PostAsJsonAsync($"api/shipments/{shipment.Id}/status",
            new ChangeShipmentStatusRequest { Status = ShipmentStatus.InTransit, Location = "Владимир" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ShipmentResponse>();
        // Начальная Created + две отметки «ВПути» — повтор статуса это трекинг, а не дубликат.
        Assert.Equal(3, body!.StatusHistory.Count);
        Assert.Equal(["Москва", "Владимир"], body.StatusHistory.Skip(1).Select(history => history.Location));
    }

    [Theory]
    [InlineData(ShipmentStatus.Created)]
    [InlineData(ShipmentStatus.Accepted)]
    public async Task ChangeStatus_ToManuallyForbiddenStatus_ReturnsBadRequest(ShipmentStatus status)
    {
        var shipment = await factory.SeedShipmentAsync(ShipmentStatus.InTransit);

        var response = await AuthenticatedClient("WarehouseOperator")
            .PostAsJsonAsync($"api/shipments/{shipment.Id}/status", new ChangeShipmentStatusRequest { Status = status });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_AfterDelivered_ReturnsConflict()
    {
        var shipment = await factory.SeedShipmentAsync(ShipmentStatus.Delivered);

        var response = await AuthenticatedClient("WarehouseOperator")
            .PostAsJsonAsync($"api/shipments/{shipment.Id}/status", new ChangeShipmentStatusRequest { Status = ShipmentStatus.InTransit });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
