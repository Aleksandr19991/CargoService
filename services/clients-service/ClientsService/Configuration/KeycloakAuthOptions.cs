namespace ClientsService.API.Configuration;

/// <summary>
/// Settings for validating Keycloak-issued JWTs — read-only, token-validation side only (no Admin
/// API client here, clients-service never creates Keycloak users, so no client secret needed).
/// </summary>
public class KeycloakAuthOptions
{
    public const string SectionName = "Keycloak";

    /// <summary>Whatever URL actually reaches Keycloak over HTTP from wherever this process runs (host vs. docker-compose network) — see CLAUDE.md.</summary>
    public required string BaseUrl { get; init; }
    public required string Realm { get; init; }
    public required string ClientId { get; init; }

    /// <summary>
    /// The `iss` claim Keycloak stamps into every token, driven by KC_HOSTNAME (docker-compose.yml).
    /// Intentionally kept separate from <see cref="BaseUrl"/> — it must match KC_HOSTNAME regardless
    /// of which URL was actually used to reach Keycloak. If you ever change KC_HOSTNAME, update this too.
    /// </summary>
    public required string ValidIssuer { get; init; }

    public string MetadataAddress => $"{BaseUrl}/realms/{Realm}/.well-known/openid-configuration";

    public static KeycloakAuthOptions Bind(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);

        return new KeycloakAuthOptions
        {
            BaseUrl = section["BaseUrl"] ?? throw new InvalidOperationException("Keycloak:BaseUrl is not configured."),
            Realm = section["Realm"] ?? throw new InvalidOperationException("Keycloak:Realm is not configured."),
            ClientId = section["ClientId"] ?? throw new InvalidOperationException("Keycloak:ClientId is not configured."),
            ValidIssuer = section["ValidIssuer"] ?? throw new InvalidOperationException("Keycloak:ValidIssuer is not configured."),
        };
    }
}
