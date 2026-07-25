using System.Security.Claims;
using System.Text.Json;
using IdentityService.Domain.Enums;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.API.Configuration;

public static class AuthenticationConfiguration
{
    public static IServiceCollection AddKeycloakAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var keycloakSection = configuration.GetSection("Keycloak");
        var keycloakBaseUrl = keycloakSection["BaseUrl"]
            ?? throw new InvalidOperationException("Keycloak:BaseUrl is not configured.");
        var keycloakRealm = keycloakSection["Realm"]
            ?? throw new InvalidOperationException("Keycloak:Realm is not configured.");
        var keycloakClientId = keycloakSection["ClientId"]
            ?? throw new InvalidOperationException("Keycloak:ClientId is not configured.");
        // The `iss` claim Keycloak stamps into tokens follows KC_HOSTNAME (docker-compose.yml), which is
        // intentionally kept stable regardless of the URL used above to actually reach Keycloak over HTTP
        // (localhost from the host, http://keycloak:8080 from inside the compose network) — see CLAUDE.md.
        var keycloakValidIssuer = keycloakSection["ValidIssuer"]
            ?? throw new InvalidOperationException("Keycloak:ValidIssuer is not configured.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MetadataAddress = $"{keycloakBaseUrl}/realms/{keycloakRealm}/.well-known/openid-configuration";
                options.RequireHttpsMetadata = false;
                options.Audience = keycloakClientId;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = keycloakValidIssuer,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = MapKeycloakRolesToRoleClaims,
                };
            });

        services.AddAuthorization();

        return services;
    }

    // Keycloak puts realm roles in a `realm_access.roles` JSON claim, not individual role claims —
    // map the ones matching our Role enum onto ClaimTypes.Role so [Authorize(Roles = ...)] works.
    private static Task MapKeycloakRolesToRoleClaims(TokenValidatedContext context)
    {
        if (context.Principal?.Identity is ClaimsIdentity identity)
        {
            var realmAccessJson = context.Principal.FindFirst("realm_access")?.Value;
            if (!string.IsNullOrEmpty(realmAccessJson))
            {
                using var document = JsonDocument.Parse(realmAccessJson);
                if (document.RootElement.TryGetProperty("roles", out var roles))
                {
                    foreach (var roleElement in roles.EnumerateArray())
                    {
                        var roleName = roleElement.GetString();
                        if (roleName is not null && Enum.TryParse<Role>(roleName, out _))
                            identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
                    }
                }
            }
        }

        return Task.CompletedTask;
    }
}
