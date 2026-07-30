using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OrdersService.Application.Interfaces;
using Testcontainers.PostgreSql;

namespace OrdersService.IntegrationTests;

/// <summary>
/// Boots the real API against a disposable Postgres container (Testcontainers) — no Keycloak,
/// RabbitMQ, or pricing-service container: <see cref="TestAuthHandler"/> replaces JwtBearer,
/// <see cref="FakePricingClient"/> replaces the real pricing.calculate HTTP call, and every
/// BackgroundService (outbox dispatcher, TariffChanged/CargoStatusChanged/PaymentCompleted
/// consumers — all need RabbitMQ) is removed entirely. These tests only exercise the API + real
/// Postgres persistence, not the external integrations.
/// </summary>
public class OrdersApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("ordersservice")
        .WithUsername("ordersservice")
        .WithPassword("ordersservice")
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
                // and FakePricingClient replaces the real pricing-service HTTP client, but these
                // config keys are still read (and required) during Program.cs startup.
                ["Keycloak:BaseUrl"] = "http://keycloak.invalid",
                ["Keycloak:Realm"] = "cargoservice",
                ["Keycloak:ClientId"] = "identity-service",
                ["Keycloak:ValidIssuer"] = "http://keycloak.invalid/realms/cargoservice",
                ["PricingService:BaseUrl"] = "http://pricingservice.invalid",
                ["RabbitMQ:HostName"] = "rabbitmq.invalid",
                ["RabbitMQ:Port"] = "5672",
                ["RabbitMQ:UserName"] = "test",
                ["RabbitMQ:Password"] = "test",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // No RabbitMQ container in these tests — the outbox dispatcher and event consumers
            // would just fail to connect and retry forever in the background.
            services.RemoveAll<IHostedService>();

            services.RemoveAll<IPricingClient>();
            services.AddScoped<IPricingClient, FakePricingClient>();

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
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
