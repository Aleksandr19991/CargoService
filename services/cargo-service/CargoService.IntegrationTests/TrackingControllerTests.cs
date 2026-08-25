using System.Net;
using System.Net.Http.Json;
using CargoService.API.Models.Requests;
using CargoService.API.Models.Responses;
using CargoService.Domain.Enums;

namespace CargoService.IntegrationTests;

public class TrackingControllerTests(CargoApiFactory factory) : IClassFixture<CargoApiFactory>
{
    [Fact]
    public async Task Track_IsAnonymousAndReturnsHistory()
    {
        var shipment = await factory.SeedShipmentAsync();

        // Без единого заголовка авторизации — эндпоинт публичный.
        var response = await factory.CreateClient().GetAsync($"api/track/{shipment.TrackingNumber}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ShipmentTrackingResponse>();
        Assert.Equal(shipment.TrackingNumber, body!.TrackingNumber);
        Assert.Equal(ShipmentStatus.Created, body.CurrentStatus);
        Assert.Single(body.History);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Track_NormalisesTrackingNumber(bool lowercase)
    {
        var shipment = await factory.SeedShipmentAsync();
        var typed = lowercase ? shipment.TrackingNumber.ToLowerInvariant() : $" {shipment.TrackingNumber} ";

        var response = await factory.CreateClient().GetAsync($"api/track/{Uri.EscapeDataString(typed)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Track_UnknownNumber_ReturnsNotFound()
    {
        var response = await factory.CreateClient().GetAsync("api/track/CS-NOSUCHNUM");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Track_DoesNotLeakInternalOrStaffOnlyData()
    {
        // Готовим груз с заведомо чувствительными данными: внутренним комментарием склада,
        // личностью приёмщика и файлами фото.
        var shipment = await factory.SeedShipmentAsync();
        var operatorId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        var staffClient = factory.CreateClient();
        staffClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "WarehouseOperator");
        staffClient.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, operatorId.ToString());

        await staffClient.PostAsJsonAsync($"api/shipments/{shipment.Id}/accept", new AcceptShipmentRequest
        {
            PackagingCondition = PackagingCondition.Damaged,
            CargoCondition = CargoCondition.Intact,
            Comment = "ВНУТРЕННЯЯ ПОМЕТКА СКЛАДА",
            PhotoFileIds = [photoId],
            PerformedPackagingTypes = [],
            Location = "Москва",
        });

        var publicJson = await factory.CreateClient().GetStringAsync($"api/track/{shipment.TrackingNumber}");

        Assert.DoesNotContain("ВНУТРЕННЯЯ ПОМЕТКА СКЛАДА", publicJson);
        Assert.DoesNotContain(operatorId.ToString(), publicJson);
        Assert.DoesNotContain(photoId.ToString(), publicJson);
        Assert.DoesNotContain(shipment.OrderId.ToString(), publicJson);
        Assert.DoesNotContain(shipment.Id.ToString(), publicJson);

        // Те же данные сотруднику видны — значит они отфильтрованы, а не потеряны.
        var staffJson = await staffClient.GetStringAsync($"api/shipments/{shipment.Id}");
        Assert.Contains("ВНУТРЕННЯЯ ПОМЕТКА СКЛАДА", staffJson);
        Assert.Contains(operatorId.ToString(), staffJson);
    }
}
