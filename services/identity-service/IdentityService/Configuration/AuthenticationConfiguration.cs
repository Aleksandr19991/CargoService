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
        var keycloak = KeycloakAuthOptions.Bind(configuration);
        var keycloakBaseUri = new Uri(keycloak.BaseUrl);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MetadataAddress = keycloak.MetadataAddress;
                // Keycloak's discovery document advertises jwks_uri using KC_HOSTNAME (same host it
                // puts in `iss` — see KeycloakAuthOptions.ValidIssuer), which is only reachable from the
                // host machine, not from inside another container. Without this, the metadata fetch
                // above succeeds but the follow-up JWKS fetch the framework makes to the *discovered*
                // jwks_uri fails, and every otherwise-valid token gets rejected with "signature key was
                // not found". Rewriting every backchannel request onto the URL we know is actually
                // reachable (keycloak.BaseUrl) sidesteps the mismatch regardless of KC_HOSTNAME's value.
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
