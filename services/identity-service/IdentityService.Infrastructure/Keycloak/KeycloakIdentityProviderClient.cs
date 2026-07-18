using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using IdentityService.Application.Interfaces;
using IdentityService.Domain.Enums;

namespace IdentityService.Infrastructure.Keycloak;

public class KeycloakIdentityProviderClient(HttpClient httpClient, KeycloakOptions options) : IIdentityProviderClient
{
    public async Task<Guid> CreateUserAsync(
        string email,
        string firstName,
        string lastName,
        string password,
        Role role,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await GetServiceAccountAccessTokenAsync(cancellationToken);
        var userId = await CreateKeycloakUserAsync(accessToken, email, firstName, lastName, password, cancellationToken);
        await AssignRealmRoleAsync(accessToken, userId, role, cancellationToken);

        return Guid.Parse(userId);
    }

    private async Task<string> GetServiceAccountAccessTokenAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, options.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret,
            }),
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>(cancellationToken);
        return token?.AccessToken
            ?? throw new InvalidOperationException("Keycloak did not return a service account access token.");
    }

    private async Task<string> CreateKeycloakUserAsync(
        string accessToken,
        string email,
        string firstName,
        string lastName,
        string password,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.AdminApiBaseUrl}/users")
        {
            Content = JsonContent.Create(new
            {
                username = email,
                email,
                firstName,
                lastName,
                enabled = true,
                emailVerified = true,
                credentials = new[]
                {
                    new { type = "password", value = password, temporary = false },
                },
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var location = response.Headers.Location
            ?? throw new InvalidOperationException("Keycloak did not return a Location header for the created user.");

        return location.Segments[^1].TrimEnd('/');
    }

    private async Task AssignRealmRoleAsync(string accessToken, string userId, Role role, CancellationToken cancellationToken)
    {
        using var getRoleRequest = new HttpRequestMessage(HttpMethod.Get, $"{options.AdminApiBaseUrl}/roles/{role}");
        getRoleRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var getRoleResponse = await httpClient.SendAsync(getRoleRequest, cancellationToken);
        getRoleResponse.EnsureSuccessStatusCode();

        var roleRepresentation = await getRoleResponse.Content.ReadFromJsonAsync<KeycloakRoleRepresentation>(cancellationToken)
            ?? throw new InvalidOperationException($"Keycloak realm role '{role}' was not found.");

        using var assignRequest = new HttpRequestMessage(HttpMethod.Post, $"{options.AdminApiBaseUrl}/users/{userId}/role-mappings/realm")
        {
            Content = JsonContent.Create(new[] { roleRepresentation }),
        };
        assignRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var assignResponse = await httpClient.SendAsync(assignRequest, cancellationToken);
        assignResponse.EnsureSuccessStatusCode();
    }

    private sealed record KeycloakTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken);

    private sealed record KeycloakRoleRepresentation(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name);
}
