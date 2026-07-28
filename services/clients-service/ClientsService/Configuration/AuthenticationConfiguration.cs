using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ClientsService.API.Configuration;

public static class AuthenticationConfiguration
{
    public static IServiceCollection AddKeycloakAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var keycloak = KeycloakAuthOptions.Bind(configuration);
        var keycloakBaseUri = new Uri(keycloak.BaseUrl);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MetadataAddress = keycloak.MetadataAddress;
                // See IdentityService/Configuration/AuthenticationConfiguration.cs for why the
                // backchannel host needs rewriting (KC_HOSTNAME vs. actually-reachable BaseUrl).
                options.BackchannelHttpHandler = new KeycloakBackchannelHandler(keycloakBaseUri);
                options.RequireHttpsMetadata = false;
                options.Audience = keycloak.ClientId;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = keycloak.ValidIssuer,
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
    // map them onto ClaimTypes.Role so [Authorize(Roles = ...)] works. Unlike identity-service,
    // there's no local Role enum here to filter against — clients-service only cares about the
    // "Client" role and copying the rest across unfiltered is harmless.
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
                        if (roleName is not null)
                            identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
                    }
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>Forces every outgoing metadata/JWKS request onto <paramref name="reachableBaseUri"/>, ignoring whatever host the request was originally addressed to.</summary>
    private sealed class KeycloakBackchannelHandler(Uri reachableBaseUri) : DelegatingHandler(new HttpClientHandler())
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.RequestUri = new UriBuilder(request.RequestUri!)
            {
                Scheme = reachableBaseUri.Scheme,
                Host = reachableBaseUri.Host,
                Port = reachableBaseUri.Port,
            }.Uri;

            return base.SendAsync(request, cancellationToken);
        }
    }
}
