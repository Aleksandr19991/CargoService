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
}
