using IdentityService.Application.Interfaces;
using IdentityService.Domain.Entities;

namespace IdentityService.Application;

public class UsersService(IUsersRepository usersRepository) : IUsersService
{
    public async Task<User> CreateUserAsync(User user, CancellationToken cancellationToken = default)
    {
        user.Id = Guid.NewGuid();
        return await usersRepository.CreateAsync(user, cancellationToken);
    }

    public Task<User?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return usersRepository.GetByIdAsync(id, cancellationToken);
    }

    public Task<List<User>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        return usersRepository.GetAllAsync(cancellationToken);
    }

    public async Task<bool> UpdateUserAsync(Guid id, User user, CancellationToken cancellationToken = default)
    {
        var existingUser = await usersRepository.GetByIdAsync(id, cancellationToken);
        if (existingUser is null)
            return false;

        existingUser.Name = user.Name;
        existingUser.LastName = user.LastName;
        existingUser.Phone = user.Phone;
        existingUser.Email = user.Email;

        return await usersRepository.UpdateAsync(existingUser, cancellationToken);
    }

    public Task<bool> DeleteUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return usersRepository.DeleteAsync(id, cancellationToken);
    }
}
