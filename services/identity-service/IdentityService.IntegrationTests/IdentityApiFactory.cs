using IdentityService.Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace IdentityService.IntegrationTests;

/// <summary>
/// Boots the real API against a disposable Postgres container (Testcontainers) — no Keycloak or
/// RabbitMQ container, since <see cref="FakeIdentityProviderClient"/> and <see cref="TestAuthHandler"/>
/// stand in for those, and the outbox dispatcher (needs RabbitMQ) is removed entirely; these tests
/// only exercise the API + real Postgres persistence, not the external integrations.
/// </summary>
public class IdentityApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("cargoservice")
        .WithUsername("cargoservice")
        .WithPassword("cargoservice")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                // Never actually reached: TestAuthHandler replaces JwtBearer as the default scheme
                // and FakeIdentityProviderClient replaces the Keycloak Admin API client, but these
                // config keys are still read (and required) during Program.cs startup.
                ["Keycloak:BaseUrl"] = "http://keycloak.invalid",
                ["Keycloak:Realm"] = "cargoservice",
                ["Keycloak:ClientId"] = "identity-service",
                ["Keycloak:ClientSecret"] = "test-secret",
                ["Keycloak:ValidIssuer"] = "http://keycloak.invalid/realms/cargoservice",
                ["RabbitMQ:HostName"] = "rabbitmq.invalid",
                ["RabbitMQ:Port"] = "5672",
                ["RabbitMQ:UserName"] = "test",
                ["RabbitMQ:Password"] = "test",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // No RabbitMQ container in these tests — the dispatcher would just fail to connect
            // and retry forever in the background.
            services.RemoveAll<IHostedService>();

            services.RemoveAll<IIdentityProviderClient>();
            services.AddScoped<IIdentityProviderClient, FakeIdentityProviderClient>();

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
