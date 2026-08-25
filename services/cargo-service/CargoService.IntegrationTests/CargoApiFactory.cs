using CargoService.Domain.Entities;
using CargoService.Domain.Enums;
using CargoService.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace CargoService.IntegrationTests;

/// <summary>
/// Boots the real API against a disposable Postgres container (Testcontainers) — no Keycloak or
/// RabbitMQ container: <see cref="TestAuthHandler"/> replaces JwtBearer, and every BackgroundService
/// (OrderConfirmed/PackageIntegrityAssessed consumers, outbox dispatcher, SLA monitor) is removed.
/// Since the OrderConfirmed consumer is gone, shipments are seeded straight through the DbContext
/// by <see cref="SeedShipmentAsync"/>.
/// </summary>
public class CargoApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("cargoshipments")
        .WithUsername("cargoshipments")
        .WithPassword("cargoshipments")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                // Never actually reached: TestAuthHandler replaces JwtBearer as the default scheme,
                // but these config keys are still read (and required) during Program.cs startup.
                ["Keycloak:BaseUrl"] = "http://keycloak.invalid",
                ["Keycloak:Realm"] = "cargoservice",
                ["Keycloak:ClientId"] = "identity-service",
                ["Keycloak:ValidIssuer"] = "http://keycloak.invalid/realms/cargoservice",
                ["RabbitMQ:HostName"] = "rabbitmq.invalid",
                ["RabbitMQ:Port"] = "5672",
                ["RabbitMQ:UserName"] = "test",
                ["RabbitMQ:Password"] = "test",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // No RabbitMQ container in these tests — consumers and the dispatcher would just fail
            // to connect and retry forever in the background. The SLA monitor goes with them: its
            // timing is covered by unit tests, and here it would mutate rows mid-test.
            services.RemoveAll<IHostedService>();

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    /// <summary>Кладёт готовый груз прямо в БД — consumer OrderConfirmed в тестах отключён.</summary>
    public async Task<Shipment> SeedShipmentAsync(
        ShipmentStatus status = ShipmentStatus.Created,
        DateTimeOffset? deliveryDeadline = null)
    {
        var now = DateTimeOffset.UtcNow;
        var shipment = new Shipment
        {
            Id = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            TrackingNumber = $"CS-{Guid.NewGuid():N}"[..13].ToUpperInvariant(),
            CurrentStatus = status,
            CreatedAt = now,
            DeliveryDeadline = deliveryDeadline,
        };
        shipment.StatusHistory.Add(new ShipmentStatusHistory { Status = ShipmentStatus.Created, ChangedAt = now });

        await using var context = CreateDbContext();
        context.Shipments.Add(shipment);
        await context.SaveChangesAsync();

        return shipment;
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
        await _postgres.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.StopAsync();
        await base.DisposeAsync();
    }
}
