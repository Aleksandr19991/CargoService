using IdentityService.Application.Interfaces;
using IdentityService.Infrastructure.Keycloak;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Infrastructure.Configuration;

public static class ServicesConfiguration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(KeycloakOptions.SectionName);
        var keycloakOptions = new KeycloakOptions
        {
            BaseUrl = section["BaseUrl"] ?? throw new InvalidOperationException("Keycloak:BaseUrl is not configured."),
            Realm = section["Realm"] ?? throw new InvalidOperationException("Keycloak:Realm is not configured."),
            ClientId = section["ClientId"] ?? throw new InvalidOperationException("Keycloak:ClientId is not configured."),
            ClientSecret = section["ClientSecret"] ?? throw new InvalidOperationException("Keycloak:ClientSecret is not configured."),
        };

        services.AddSingleton(keycloakOptions);
        services.AddHttpClient<IIdentityProviderClient, KeycloakIdentityProviderClient>();

        return services;
    }
}
