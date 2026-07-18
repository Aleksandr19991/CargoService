namespace IdentityService.Infrastructure.Keycloak;

public class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    public required string BaseUrl { get; init; }
    public required string Realm { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }

    /// <summary>Base URL used for reaching Keycloak over HTTP (discovery, admin API). Not necessarily what ends up in issued tokens' `iss` claim — see Keycloak:ValidIssuer.</summary>
    public string Authority => $"{BaseUrl}/realms/{Realm}";

    public string TokenEndpoint => $"{Authority}/protocol/openid-connect/token";

    public string AdminApiBaseUrl => $"{BaseUrl}/admin/realms/{Realm}";
}
