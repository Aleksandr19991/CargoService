using System.Text.Json;
using CargoService.Contracts.Events.V1;
using CargoService.Contracts.Messaging;
using IdentityService.Application.Interfaces;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Enums;

namespace IdentityService.Application;

public class UsersService(
    IUsersRepository usersRepository,
    IIdentityProviderClient identityProviderClient,
    IOutboxWriter outboxWriter) : IUsersService
{
    private const string ServiceName = "identity-service";

    public async Task<User> CreateUserAsync(User user, string password, CancellationToken cancellationToken = default)
    {
        user.Id = await identityProviderClient.CreateUserAsync(
            user.Email, user.Name, user.LastName, password, user.Role, cancellationToken);

        // Only self-registered clients matter to downstream consumers (e.g. clients-service
        // auto-creating a ClientAccount) — staff accounts created via api/users/staff aren't clients.
        if (user.Role == Role.Client)
            EnqueueUserRegisteredEvent(user);

        // Enqueue() above only stages the outbox row on the same (scoped) DbContext that
        // CreateAsync below commits via SaveChangesAsync — this is what makes the user row and
        // the outbox row land in the same database transaction.
        return await usersRepository.CreateAsync(user, cancellationToken);
    }

    private void EnqueueUserRegisteredEvent(User user)
    {
        var integrationEvent = new UserRegistered
        {
            UserId = user.Id,
            Name = user.Name,
            LastName = user.LastName,
            Phone = user.Phone,
            Email = user.Email,
        };

        var routingKey = RabbitMqConventions.RoutingKey(ServiceName, nameof(UserRegistered));
        var payloadJson = JsonSerializer.Serialize(integrationEvent);

        outboxWriter.Enqueue(integrationEvent.EventId, routingKey, payloadJson, integrationEvent.OccurredAtUtc);
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
