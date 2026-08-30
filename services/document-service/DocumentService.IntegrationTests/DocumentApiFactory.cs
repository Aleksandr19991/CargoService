using DocumentService.Application.Interfaces;
using DocumentService.Domain.Entities;
using DocumentService.Domain.Enums;
using DocumentService.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace DocumentService.IntegrationTests;

/// <summary>
/// Поднимает настоящий API поверх одноразового контейнера Postgres (Testcontainers). Ни Keycloak,
/// ни RabbitMQ, ни MinIO не нужны: <see cref="TestAuthHandler"/> заменяет JwtBearer, все
/// <see cref="IHostedService"/> (консьюмеры, печать, outbox) сняты, а клиент хранилища подменён
/// заглушкой — проверяется API выдачи, а не работа с файлами.
/// </summary>
public class DocumentApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string StubDownloadUrl = "http://storage.test/signed-link";

    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("documentservice")
        .WithUsername("documentservice")
        .WithPassword("documentservice")
        .Build();

    /// <summary>Файлы, которых «нет» в хранилище: по ним заглушка вернёт <c>null</c>.</summary>
    public HashSet<Guid> MissingFiles { get; } = [];

    public string ConnectionString => postgres.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting, а не ConfigureAppConfiguration: Program.cs читает строку подключения и
        // остальные секции из builder.Configuration ДО builder.Build(), а источники из
        // ConfigureAppConfiguration подмешиваются только при построении хоста — слишком поздно,
        // и тест молча ушёл бы на адрес из appsettings.json (см. Фазу 6, задача 7).
        var overrides = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = postgres.GetConnectionString(),
            ["Keycloak:BaseUrl"] = "http://keycloak.invalid",
            ["Keycloak:Realm"] = "cargoservice",
            ["Keycloak:ClientId"] = "identity-service",
            ["Keycloak:ValidIssuer"] = "http://keycloak.invalid/realms/cargoservice",
            ["KeycloakServiceAccount:BaseUrl"] = "http://keycloak.invalid",
            ["KeycloakServiceAccount:Realm"] = "cargoservice",
            ["KeycloakServiceAccount:ClientId"] = "document-service",
            ["KeycloakServiceAccount:ClientSecret"] = "test",
            ["FileStorageService:BaseUrl"] = "http://filestorage.invalid",
            ["RabbitMQ:HostName"] = "rabbitmq.invalid",
            ["RabbitMQ:Port"] = "5672",
            ["RabbitMQ:UserName"] = "test",
            ["RabbitMQ:Password"] = "test",
        };

        foreach (var (key, value) in overrides)
            builder.UseSetting(key, value);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();

            services.RemoveAll<IFileStorageClient>();
            services.AddSingleton<IFileStorageClient>(new StubFileStorageClient(MissingFiles));

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    /// <summary>Кладёт документ и сведения о заявке прямо в БД — консьюмеры в тестах сняты.</summary>
    public async Task<Document> SeedDocumentAsync(
        Guid ownerId,
        Guid orderId,
        Guid shipmentId,
        DocumentStatus status = DocumentStatus.Ready,
        DocumentType type = DocumentType.Waybill,
        bool withSnapshot = true)
    {
        var now = DateTimeOffset.UtcNow;
        var document = new Document
        {
            Id = Guid.NewGuid(),
            Type = type,
            Status = status,
            ShipmentId = shipmentId,
            OrderId = orderId,
            TrackingNumber = "CS-TEST000001",
            IssuedAt = now,
            CreatedAt = now,
            FileId = status == DocumentStatus.Ready ? Guid.NewGuid() : null,
            GeneratedAt = status == DocumentStatus.Ready ? now : null,
            FailureReason = status == DocumentStatus.Failed ? "хранилище недоступно" : null,
        };

        await using var context = CreateDbContext();
        context.Documents.Add(document);

        if (withSnapshot && !await context.OrderSnapshots.AnyAsync(snapshot => snapshot.OrderId == orderId))
        {
            context.OrderSnapshots.Add(new OrderSnapshot
            {
                OrderId = orderId,
                ClientAccountId = ownerId,
                OrderNumber = "20260830-TEST01",
                OriginCity = "Москва",
                DestinationCity = "Владимир",
                SenderName = "ООО «Отправитель»",
                RecipientName = "Сидоров Иван Петрович",
                CargoName = "Шкаф-купе",
            });
        }

        await context.SaveChangesAsync();

        return document;
    }

    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new AppDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();

        // Схему создаёт хост при старте (MigrateDatabaseAsync), а WebApplicationFactory поднимает
        // его лениво — без этой строки тест, засеивающий документ до первого запроса, падает на
        // «relation documents does not exist».
        using var _ = CreateClient();
    }

    public new async Task DisposeAsync()
    {
        await postgres.StopAsync();
        await base.DisposeAsync();
    }

    private sealed class StubFileStorageClient(HashSet<Guid> missingFiles) : IFileStorageClient
    {
        public Task<Guid> UploadAsync(byte[] content, string contentType, CancellationToken cancellationToken) =>
            Task.FromResult(Guid.NewGuid());

        public Task<DocumentDownloadLink?> CreateDownloadLinkAsync(Guid fileId, CancellationToken cancellationToken) =>
            Task.FromResult(missingFiles.Contains(fileId)
                ? null
                : new DocumentDownloadLink(StubDownloadUrl, DateTimeOffset.UtcNow.AddMinutes(15)));
    }
}
