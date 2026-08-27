using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Persistence;
using Testcontainers.PostgreSql;

namespace NotificationService.IntegrationTests;

/// <summary>
/// Поднимает настоящий API поверх одноразового контейнера Postgres (Testcontainers). Ни Keycloak,
/// ни RabbitMQ, ни SMTP не нужны: <see cref="TestAuthHandler"/> заменяет JwtBearer, а все
/// <see cref="IHostedService"/> (консьюмеры событий) сняты — иначе они бесконечно переподключались
/// бы к несуществующему брокеру. Раз консьюмеров нет, история и контакты кладутся прямо через
/// <see cref="AppDbContext"/>.
/// </summary>
public class NotificationApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("notificationservice")
        .WithUsername("notificationservice")
        .WithPassword("notificationservice")
        .Build();

    public string ConnectionString => postgres.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting, а не ConfigureAppConfiguration: Program.cs читает строку подключения (и
        // секции Keycloak/RabbitMQ/Smtp) из builder.Configuration ДО builder.Build(), а источники,
        // добавленные через ConfigureAppConfiguration, подмешиваются только на этапе построения
        // хоста — то есть слишком поздно. Тест в этом случае молча уходил бы на адрес из
        // appsettings.json вместо контейнера Testcontainers. UseSetting кладёт значение в
        // конфигурацию хоста, доступную с самого начала.
        var overrides = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = postgres.GetConnectionString(),
            // До этих адресов никто не ходит: аутентификацию подменяет TestAuthHandler,
            // консьюмеры сняты, отправители в клиентском API не участвуют. Но ключи читаются
            // при старте Program.cs, и без них хост не поднимется.
            ["Keycloak:BaseUrl"] = "http://keycloak.invalid",
            ["Keycloak:Realm"] = "cargoservice",
            ["Keycloak:ClientId"] = "identity-service",
            ["Keycloak:ValidIssuer"] = "http://keycloak.invalid/realms/cargoservice",
            ["Smtp:Host"] = "smtp.invalid",
            ["Smtp:Port"] = "1025",
            ["Smtp:UseStartTls"] = "false",
            ["Smtp:FromAddress"] = "no-reply@cargoservice.local",
            ["Smtp:FromName"] = "CargoService",
            ["Sms:BaseUrl"] = "http://sms.invalid",
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

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    /// <summary>Кладёт запись истории прямо в БД — консьюмеры событий в тестах отключены.</summary>
    public async Task<NotificationLog> SeedLogAsync(
        Guid recipientUserId,
        NotificationChannel channel = NotificationChannel.Email,
        string templateCode = "CargoStatusChanged",
        NotificationStatus status = NotificationStatus.Sent,
        DateTimeOffset? createdAt = null)
    {
        var timestamp = createdAt ?? DateTimeOffset.UtcNow;
        var log = new NotificationLog
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            RecipientContact = channel == NotificationChannel.Email ? "client@example.com" : "+79990000001",
            Channel = channel,
            TemplateCode = templateCode,
            Subject = channel == NotificationChannel.Email ? "Груз CS-ABC123: статус «InTransit»" : null,
            Body = "Груз CS-ABC123: InTransit.",
            Status = status,
            CreatedAt = timestamp,
            SentAt = status == NotificationStatus.Sent ? timestamp : null,
            FailureReason = status == NotificationStatus.Failed ? "SMTP: mailbox unavailable" : null,
            RelatedEntityType = RelatedEntityType.Shipment,
            RelatedEntityId = Guid.NewGuid(),
        };

        await using var context = CreateDbContext();
        context.NotificationLogs.Add(log);
        await context.SaveChangesAsync();

        return log;
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

        // Схему создаёт сам хост при старте (MigrateDatabaseAsync в Program.cs), а
        // WebApplicationFactory поднимает его лениво — при первом обращении к клиенту или
        // сервисам. Без этой строки тест, который сначала засеивает историю, а потом делает
        // запрос, падает на «relation notification_logs does not exist».
        using var _ = CreateClient();
    }

    public new async Task DisposeAsync()
    {
        await postgres.StopAsync();
        await base.DisposeAsync();
    }
}
