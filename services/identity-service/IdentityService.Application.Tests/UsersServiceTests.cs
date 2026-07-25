using IdentityService.Application.Interfaces;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Enums;
using Moq;

namespace IdentityService.Application.Tests;

public class UsersServiceTests
{
    private readonly Mock<IUsersRepository> _usersRepository = new();
    private readonly Mock<IIdentityProviderClient> _identityProviderClient = new();
    private readonly Mock<IOutboxWriter> _outboxWriter = new();
    private readonly UsersService _sut;

    public UsersServiceTests()
    {
        _sut = new UsersService(_usersRepository.Object, _identityProviderClient.Object, _outboxWriter.Object);
    }

    [Fact]
    public async Task CreateUserAsync_AssignsIdReturnedByIdentityProvider()
    {
        var providerAssignedId = Guid.NewGuid();
        _identityProviderClient
            .Setup(client => client.CreateUserAsync(
                "jane@example.com", "Jane", "Doe", "s3cret", Role.Client, It.IsAny<CancellationToken>()))
            .ReturnsAsync(providerAssignedId);
        _usersRepository
            .Setup(repository => repository.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User user, CancellationToken _) => user);

        var user = new User { Name = "Jane", LastName = "Doe", Email = "jane@example.com", Role = Role.Client };

        var result = await _sut.CreateUserAsync(user, "s3cret");

        Assert.Equal(providerAssignedId, result.Id);
        _usersRepository.Verify(
            repository => repository.CreateAsync(
                It.Is<User>(u => u.Id == providerAssignedId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_ClientRole_EnqueuesUserRegisteredOutboxMessage()
    {
        _identityProviderClient
            .Setup(client => client.CreateUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), Role.Client, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());
        _usersRepository
            .Setup(repository => repository.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User user, CancellationToken _) => user);

        var user = new User { Name = "Jane", LastName = "Doe", Email = "jane@example.com", Role = Role.Client };

        await _sut.CreateUserAsync(user, "s3cret");

        _outboxWriter.Verify(
            writer => writer.Enqueue(
                It.IsAny<Guid>(),
                "identity-service.user-registered",
                It.Is<string>(json => json.Contains("jane@example.com")),
                It.IsAny<DateTimeOffset>()),
            Times.Once);
    }

    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.WarehouseOperator)]
    [InlineData(Role.Courier)]
    [InlineData(Role.Admin)]
    public async Task CreateUserAsync_NonClientRole_DoesNotEnqueueOutboxMessage(Role role)
    {
        _identityProviderClient
            .Setup(client => client.CreateUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), role, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());
        _usersRepository
            .Setup(repository => repository.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User user, CancellationToken _) => user);

        var user = new User { Name = "Jane", LastName = "Doe", Email = "jane@example.com", Role = role };

        await _sut.CreateUserAsync(user, "s3cret");

        _outboxWriter.Verify(
            writer => writer.Enqueue(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_UserDoesNotExist_ReturnsFalseWithoutUpdating()
    {
        _usersRepository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _sut.UpdateUserAsync(Guid.NewGuid(), new User());

        Assert.False(result);
        _usersRepository.Verify(
            repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_UserExists_UpdatesProfileFieldsOnly()
    {
        var id = Guid.NewGuid();
        var existingUser = new User
        {
            Id = id,
            Name = "Old",
            LastName = "Name",
            Phone = "111",
            Email = "old@example.com",
            Role = Role.Admin,
            IsDeactivated = true,
        };

        _usersRepository
            .Setup(repository => repository.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);
        _usersRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var update = new User { Name = "New", LastName = "Name2", Phone = "222", Email = "new@example.com" };

        var result = await _sut.UpdateUserAsync(id, update);

        Assert.True(result);
        _usersRepository.Verify(
            repository => repository.UpdateAsync(
                It.Is<User>(u =>
                    u.Id == id &&
                    u.Name == "New" &&
                    u.LastName == "Name2" &&
                    u.Phone == "222" &&
                    u.Email == "new@example.com" &&
                    u.Role == Role.Admin), // Role isn't touched by UpdateUserAsync.
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ChangeUserRoleAsync_UserDoesNotExist_ReturnsNull()
    {
        _usersRepository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _sut.ChangeUserRoleAsync(Guid.NewGuid(), Role.Manager);

        Assert.Null(result);
        _identityProviderClient.Verify(
            client => client.ChangeUserRoleAsync(
                It.IsAny<Guid>(), It.IsAny<Role>(), It.IsAny<Role>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _usersRepository.Verify(
            repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangeUserRoleAsync_NewRoleMatchesCurrentRole_ReturnsUserWithoutCallingIdentityProvider()
    {
        var id = Guid.NewGuid();
        var existingUser = new User { Id = id, Role = Role.Manager };

        _usersRepository
            .Setup(repository => repository.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var result = await _sut.ChangeUserRoleAsync(id, Role.Manager);

        Assert.Same(existingUser, result);
        _identityProviderClient.Verify(
            client => client.ChangeUserRoleAsync(
                It.IsAny<Guid>(), It.IsAny<Role>(), It.IsAny<Role>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _usersRepository.Verify(
            repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangeUserRoleAsync_RoleChanges_UpdatesIdentityProviderAndRepository()
    {
        var id = Guid.NewGuid();
        var existingUser = new User { Id = id, Role = Role.WarehouseOperator };

        _usersRepository
            .Setup(repository => repository.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);
        _usersRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.ChangeUserRoleAsync(id, Role.Manager);

        Assert.NotNull(result);
        Assert.Equal(Role.Manager, result!.Role);
        _identityProviderClient.Verify(
            client => client.ChangeUserRoleAsync(id, Role.WarehouseOperator, Role.Manager, It.IsAny<CancellationToken>()),
            Times.Once);
        _usersRepository.Verify(
            repository => repository.UpdateAsync(
                It.Is<User>(u => u.Id == id && u.Role == Role.Manager), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteUserAsync_DelegatesToRepository()
    {
        var id = Guid.NewGuid();
        _usersRepository
            .Setup(repository => repository.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.DeleteUserAsync(id);

        Assert.True(result);
        _usersRepository.Verify(repository => repository.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
