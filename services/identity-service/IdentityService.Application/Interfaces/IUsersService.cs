using IdentityService.Domain.Entities;
using IdentityService.Domain.Enums;

namespace IdentityService.Application.Interfaces;

public interface IUsersService
{
    Task<User> CreateUserAsync(User user, string password, CancellationToken cancellationToken = default);
    Task<User?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<User>> GetAllUsersAsync(CancellationToken cancellationToken = default);
    Task<bool> UpdateUserAsync(Guid id, User user, CancellationToken cancellationToken = default);
    Task<User?> ChangeUserRoleAsync(Guid id, Role newRole, CancellationToken cancellationToken = default);
    Task<bool> DeleteUserAsync(Guid id, CancellationToken cancellationToken = default);
}
