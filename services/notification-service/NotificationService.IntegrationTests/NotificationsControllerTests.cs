using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using NotificationService.Domain.Enums;

namespace NotificationService.IntegrationTests;

public class NotificationsControllerTests(NotificationApiFactory factory) : IClassFixture<NotificationApiFactory>
{
    // API отдаёт enum'ы строками (JsonStringEnumConverter в AddApiServices) — читаем ответы теми
    // же правилами, что и настоящий клиент.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task GetNotifications_WithoutToken_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetNotifications_AsStaff_ReturnsForbidden()
    {
        // История — личный кабинет клиента; служебные алерты владельца-пользователя не имеют,
        // и сотруднику здесь смотреть нечего.
        var client = CreateClient(Guid.NewGuid(), "Admin");

        var response = await client.GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetNotifications_ReturnsOnlyOwnHistoryNewestFirst()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await factory.SeedLogAsync(userId, createdAt: now.AddMinutes(-10), templateCode: "OrderCreated");
        await factory.SeedLogAsync(userId, createdAt: now, templateCode: "CargoStatusChanged");
        await factory.SeedLogAsync(otherUserId, createdAt: now, templateCode: "CargoStatusChanged");

        var history = await CreateClient(userId).GetFromJsonAsync<NotificationListDto>("/api/notifications", JsonOptions);

        Assert.NotNull(history);
        Assert.Equal(2, history.TotalCount);
        Assert.Equal("CargoStatusChanged", history.Items[0].TemplateCode);
        Assert.Equal("OrderCreated", history.Items[1].TemplateCode);
    }

    [Fact]
    public async Task GetNotifications_DoesNotExposeMessageBody()
    {
        var userId = Guid.NewGuid();
        await factory.SeedLogAsync(userId);

        var payload = await CreateClient(userId).GetStringAsync("/api/notifications");

        // Тело отправленного сообщения в списке не отдаётся — см. докблок NotificationResponse.
        Assert.DoesNotContain("\"body\"", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"subject\"", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetNotifications_FiltersByChannel()
    {
        var userId = Guid.NewGuid();
        await factory.SeedLogAsync(userId, NotificationChannel.Email);
        await factory.SeedLogAsync(userId, NotificationChannel.Sms);

        var history = await CreateClient(userId).GetFromJsonAsync<NotificationListDto>("/api/notifications?channel=Sms", JsonOptions);

        Assert.NotNull(history);
        Assert.Equal(1, history.TotalCount);
        Assert.Equal(NotificationChannel.Sms, history.Items[0].Channel);
    }

    [Fact]
    public async Task GetNotifications_Paginates()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await factory.SeedLogAsync(userId, createdAt: now.AddMinutes(-2));
        await factory.SeedLogAsync(userId, createdAt: now.AddMinutes(-1));
        await factory.SeedLogAsync(userId, createdAt: now);

        var client = CreateClient(userId);
        var firstPage = await client.GetFromJsonAsync<NotificationListDto>("/api/notifications?page=1&pageSize=2", JsonOptions);
        var secondPage = await client.GetFromJsonAsync<NotificationListDto>("/api/notifications?page=2&pageSize=2", JsonOptions);

        Assert.NotNull(firstPage);
        Assert.NotNull(secondPage);
        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Single(secondPage.Items);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=500")]
    public async Task GetNotifications_RejectsInvalidPaging(string query)
    {
        var response = await CreateClient(Guid.NewGuid()).GetAsync($"/api/notifications{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPreferences_ReturnsDefaultsWhenNeverSaved()
    {
        var preferences = await CreateClient(Guid.NewGuid())
            .GetFromJsonAsync<PreferencesDto>("/api/notifications/preferences", JsonOptions);

        Assert.NotNull(preferences);
        Assert.True(preferences.EmailEnabled);
        Assert.True(preferences.SmsEnabled);
    }

    [Fact]
    public async Task PostPreferences_SavesAgainstTokenUser()
    {
        var userId = Guid.NewGuid();
        var client = CreateClient(userId);

        var response = await client.PostAsJsonAsync(
            "/api/notifications/preferences",
            new { emailEnabled = true, smsEnabled = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stored = await client.GetFromJsonAsync<PreferencesDto>("/api/notifications/preferences", JsonOptions);
        Assert.NotNull(stored);
        Assert.True(stored.EmailEnabled);
        Assert.False(stored.SmsEnabled);

        // Настройки записаны на пользователя из токена, а не на что-то, присланное в теле.
        await using var context = factory.CreateDbContext();
        var row = await context.NotificationPreferences.SingleAsync(preference => preference.UserId == userId);
        Assert.False(row.SmsEnabled);
    }

    [Fact]
    public async Task PostPreferences_DoesNotAffectOtherClients()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        await CreateClient(userId).PostAsJsonAsync(
            "/api/notifications/preferences",
            new { emailEnabled = false, smsEnabled = false });

        var others = await CreateClient(otherUserId)
            .GetFromJsonAsync<PreferencesDto>("/api/notifications/preferences", JsonOptions);

        Assert.NotNull(others);
        Assert.True(others.EmailEnabled);
        Assert.True(others.SmsEnabled);
    }

    [Fact]
    public async Task PostPreferences_RejectsIncompleteBody()
    {
        // Настройки сохраняются целиком: половина набора — ошибка, а не частичная правка.
        var response = await CreateClient(Guid.NewGuid())
            .PostAsJsonAsync("/api/notifications/preferences", new { emailEnabled = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient CreateClient(Guid userId, string roles = "Client")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return client;
    }

    private sealed record NotificationListDto(List<NotificationDto> Items, int TotalCount, int Page, int PageSize);

    private sealed record NotificationDto(Guid Id, NotificationChannel Channel, string TemplateCode, NotificationStatus Status);

    private sealed record PreferencesDto(bool EmailEnabled, bool SmsEnabled);
}
