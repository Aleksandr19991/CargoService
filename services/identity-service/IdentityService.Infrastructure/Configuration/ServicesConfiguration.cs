using IdentityService.Application.Interfaces;
using IdentityService.Infrastructure.Keycloak;
using IdentityService.Infrastructure.Messaging;
using IdentityService.Infrastructure.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Infrastructure.Configuration;

public static class ServicesConfiguration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var keycloakSection = configuration.GetSection(KeycloakOptions.SectionName);
        var keycloakOptions = new KeycloakOptions
        {
            BaseUrl = keycloakSection["BaseUrl"] ?? throw new InvalidOperationException("Keycloak:BaseUrl is not configured."),
            Realm = keycloakSection["Realm"] ?? throw new InvalidOperationException("Keycloak:Realm is not configured."),
            ClientId = keycloakSection["ClientId"] ?? throw new InvalidOperationException("Keycloak:ClientId is not configured."),
            ClientSecret = keycloakSection["ClientSecret"] ?? throw new InvalidOperationException("Keycloak:ClientSecret is not configured."),
        };

        services.AddSingleton(keycloakOptions);
        services.AddHttpClient<IIdentityProviderClient, KeycloakIdentityProviderClient>();

        var rabbitMqSection = configuration.GetSection(RabbitMqOptions.SectionName);
        var rabbitMqOptions = new RabbitMqOptions
        {
            HostName = rabbitMqSection["HostName"] ?? throw new InvalidOperationException("RabbitMQ:HostName is not configured."),
            Port = int.Parse(rabbitMqSection["Port"] ?? throw new InvalidOperationException("RabbitMQ:Port is not configured.")),
            UserName = rabbitMqSection["UserName"] ?? throw new InvalidOperationException("RabbitMQ:UserName is not configured."),
            Password = rabbitMqSection["Password"] ?? throw new InvalidOperationException("RabbitMQ:Password is not configured."),
        };

        services.AddSingleton(rabbitMqOptions);
        services.AddHostedService<OutboxDispatcher>();

        return services;
    }
}
