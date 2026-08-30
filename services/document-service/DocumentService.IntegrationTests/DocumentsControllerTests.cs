using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentService.Domain.Enums;

namespace DocumentService.IntegrationTests;

public class DocumentsControllerTests(DocumentApiFactory factory) : IClassFixture<DocumentApiFactory>
{
    // API отдаёт enum'ы строками (JsonStringEnumConverter в AddApiServices) — читаем ответы теми
    // же правилами, что и настоящий клиент.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task GetDocuments_WithoutToken_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync($"/api/documents?orderId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?orderId=11111111-1111-1111-1111-111111111111&shipmentId=22222222-2222-2222-2222-222222222222")]
    [InlineData("?orderId=00000000-0000-0000-0000-000000000000")]
    public async Task GetDocuments_RejectsAmbiguousQuery(string query)
    {
        var response = await CreateClient(Guid.NewGuid()).GetAsync($"/api/documents{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetDocuments_ReturnsOwnDocumentsByOrder()
    {
        var owner = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        await factory.SeedDocumentAsync(owner, orderId, Guid.NewGuid());

        var documents = await CreateClient(owner)
            .GetFromJsonAsync<List<DocumentDto>>($"/api/documents?orderId={orderId}", JsonOptions);

        Assert.NotNull(documents);
        var document = Assert.Single(documents);
        Assert.Equal(DocumentType.Waybill, document.Type);
        Assert.Equal(DocumentStatus.Ready, document.Status);
        Assert.NotNull(document.FileId);
    }

    [Fact]
    public async Task GetDocuments_HidesDocumentsFromOtherClients()
    {
        var orderId = Guid.NewGuid();
        await factory.SeedDocumentAsync(Guid.NewGuid(), orderId, Guid.NewGuid());

        // Пустой список, а не 403: иначе по коду ответа можно было бы выяснять, есть ли такая заявка.
        var documents = await CreateClient(Guid.NewGuid())
            .GetFromJsonAsync<List<DocumentDto>>($"/api/documents?orderId={orderId}", JsonOptions);

        Assert.NotNull(documents);
        Assert.Empty(documents);
    }

    [Fact]
    public async Task GetDocuments_ReturnsAnyDocumentsToStaffByShipment()
    {
        var shipmentId = Guid.NewGuid();
        await factory.SeedDocumentAsync(Guid.NewGuid(), Guid.NewGuid(), shipmentId);

        var documents = await CreateClient(Guid.NewGuid(), "Manager")
            .GetFromJsonAsync<List<DocumentDto>>($"/api/documents?shipmentId={shipmentId}", JsonOptions);

        Assert.NotNull(documents);
        Assert.Single(documents);
    }

    [Fact]
    public async Task GetDocument_ReturnsNotFoundForOtherClients()
    {
        var document = await factory.SeedDocumentAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var response = await CreateClient(Guid.NewGuid()).GetAsync($"/api/documents/{document.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDownloadUrl_ReturnsSignedLinkForOwner()
    {
        var owner = Guid.NewGuid();
        var document = await factory.SeedDocumentAsync(owner, Guid.NewGuid(), Guid.NewGuid());

        var link = await CreateClient(owner)
            .GetFromJsonAsync<DownloadDto>($"/api/documents/{document.Id}/download-url", JsonOptions);

        Assert.NotNull(link);
        Assert.Equal(document.Id, link.DocumentId);
        Assert.Equal(DocumentApiFactory.StubDownloadUrl, link.DownloadUrl);
        Assert.True(link.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task GetDownloadUrl_ReturnsConflictWhenDocumentIsNotReady()
    {
        var owner = Guid.NewGuid();
        var document = await factory.SeedDocumentAsync(
            owner, Guid.NewGuid(), Guid.NewGuid(), DocumentStatus.Failed);

        var response = await CreateClient(owner).GetAsync($"/api/documents/{document.Id}/download-url");

        // Документ есть, файла нет — это состояние документа, а не «не найдено».
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("хранилище недоступно", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetDownloadUrl_ReturnsConflictWhenFileIsGoneFromStorage()
    {
        var owner = Guid.NewGuid();
        var document = await factory.SeedDocumentAsync(owner, Guid.NewGuid(), Guid.NewGuid());
        factory.MissingFiles.Add(document.FileId!.Value);

        var response = await CreateClient(owner).GetAsync($"/api/documents/{document.Id}/download-url");

        // Ссылка, которая ответит 404, хуже честного отказа.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetDocuments_DeniesClientWhenOrderIsUnknown()
    {
        var orderId = Guid.NewGuid();
        await factory.SeedDocumentAsync(Guid.NewGuid(), orderId, Guid.NewGuid(), withSnapshot: false);

        var documents = await CreateClient(Guid.NewGuid())
            .GetFromJsonAsync<List<DocumentDto>>($"/api/documents?orderId={orderId}", JsonOptions);

        Assert.NotNull(documents);
        Assert.Empty(documents);
    }

    private HttpClient CreateClient(Guid userId, string roles = "Client")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return client;
    }

    private sealed record DocumentDto(
        Guid Id,
        DocumentType Type,
        DocumentStatus Status,
        Guid OrderId,
        Guid ShipmentId,
        Guid? FileId);

    private sealed record DownloadDto(Guid DocumentId, string DownloadUrl, DateTimeOffset ExpiresAt);
}
