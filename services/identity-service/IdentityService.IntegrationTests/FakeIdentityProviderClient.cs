using IdentityService.Application.Interfaces;
using IdentityService.Application.Models;
using IdentityService.Domain.Enums;

namespace IdentityService.IntegrationTests;

/// <summary>
/// Stands in for Keycloak in integration tests — no real IdP is available in this environment.
/// Just hands back a fresh id, mirroring the "Keycloak assigns the id" contract UsersService relies on.
/// </summary>
public class FakeIdentityProviderClient : IIdentityProviderClient
{
    public Task<Guid> CreateUserAsync(
        string email,
        string firstName,
        string lastName,
        string password,
        Role role,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Guid.NewGuid());
    }

    public Task<AuthToken?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<AuthToken?>(null);
    }

    public Task<AuthToken?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<AuthToken?>(null);
    }

    public Task ChangeUserRoleAsync(
        Guid userId,
        Role oldRole,
        Role newRole,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
