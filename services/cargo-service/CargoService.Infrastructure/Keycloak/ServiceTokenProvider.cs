using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CargoService.Infrastructure.Keycloak;

/// <summary>
/// Выдаёт токен сервисной учётной записи cargo-service для вызовов в другие сервисы
/// (client_credentials в Keycloak). Первый в проекте механизм межсервисной аутентификации:
/// до сих пор синхронным был только вызов pricing-service, а его калькулятор публичен по
/// замыслу (spec.md §3.7), так что токен там не требовался.
/// <para>
/// Токен кэшируется до истечения с запасом: Keycloak выдаёт его на 5 минут, и ходить за новым
/// на каждый вызов значило бы утроить число сетевых обращений на ровном месте.
/// </para>
/// </summary>
public class ServiceTokenProvider(HttpClient httpClient, KeycloakServiceAccountOptions options)
{
    // Запас на дорогу и рассинхрон часов: токен, которому осталось меньше, считаем просроченным.
    private static readonly TimeSpan ExpiryLeeway = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (IsUsable())
            return _accessToken!;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Пока ждали блокировку, токен мог обновить кто-то другой.
            if (IsUsable())
                return _accessToken!;

            var response = await httpClient.PostAsync(
                options.TokenEndpoint,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = options.ClientId,
                    ["client_secret"] = options.ClientSecret,
                }),
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Keycloak token response deserialized to null.");

            _accessToken = token.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);

            return _accessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool IsUsable() =>
        _accessToken is not null && DateTimeOffset.UtcNow < _expiresAt - ExpiryLeeway;

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public required string AccessToken { get; init; }

        [JsonPropertyName("expires_in")]
        public required int ExpiresIn { get; init; }
    }
}
