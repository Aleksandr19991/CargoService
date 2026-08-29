namespace AiInspectionService.Infrastructure.Keycloak;

/// <summary>
/// Учётные данные сервисной учётной записи ai-inspection-service. Отдельно от <c>Keycloak</c>-секции
/// API-слоя (та только про проверку входящих токенов и секрета не содержит) — здесь исходящая
/// сторона, и секрет ей нужен.
/// </summary>
public class KeycloakServiceAccountOptions
{
    public const string SectionName = "KeycloakServiceAccount";

    /// <summary>Адрес, по которому Keycloak реально достижим из этого процесса (см. CLAUDE.md про BaseUrl vs ValidIssuer).</summary>
    public required string BaseUrl { get; init; }
    public required string Realm { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }

    public string TokenEndpoint => $"{BaseUrl}/realms/{Realm}/protocol/openid-connect/token";
}
