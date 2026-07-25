using IdentityService.Application.Models;
using IdentityService.Domain.Enums;

namespace IdentityService.Application.Interfaces;

/// <summary>Creates and manages user accounts in the external identity provider (Keycloak).</summary>
public interface IIdentityProviderClient
{
    /// <summary>Creates the user in the identity provider and assigns the given realm role. Returns the provider-assigned user id.</summary>
    Task<Guid> CreateUserAsync(
        string email,
        string firstName,
        string lastName,
        string password,
        Role role,
        CancellationToken cancellationToken = default);

    /// <summary>Exchanges a username/password for tokens (Resource Owner Password Credentials grant). Returns null on invalid credentials.</summary>
    Task<AuthToken?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>Exchanges a refresh token for a new token pair. Returns null if the refresh token is invalid or expired.</summary>
    Task<AuthToken?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>Changes an existing user's realm role in the identity provider: removes the old role, assigns the new one.</summary>
    Task ChangeUserRoleAsync(
        Guid userId,
        Role oldRole,
        Role newRole,
        CancellationToken cancellationToken = default);
}
