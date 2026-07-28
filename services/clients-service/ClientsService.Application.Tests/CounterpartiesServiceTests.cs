using ClientsService.Application.Interfaces;
using ClientsService.Domain.Entities;
using ClientsService.Domain.Enums;
using Moq;

namespace ClientsService.Application.Tests;

public class CounterpartiesServiceTests
{
    private readonly Mock<ICounterpartiesRepository> _counterpartiesRepository = new();
    private readonly Mock<IClientAccountsRepository> _clientAccountsRepository = new();
    private readonly CounterpartiesService _sut;

    public CounterpartiesServiceTests()
    {
        _sut = new CounterpartiesService(_counterpartiesRepository.Object, _clientAccountsRepository.Object);
    }

    [Fact]
    public async Task CreateAsync_GetsOrCreatesClientAccountAndAssignsItToTheCounterparty()
    {
        var userId = Guid.NewGuid();
        var account = new ClientAccount { Id = Guid.NewGuid(), UserId = userId };
        _clientAccountsRepository
            .Setup(repository => repository.GetOrCreateByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _counterpartiesRepository
            .Setup(repository => repository.CreateAsync(It.IsAny<Counterparty>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Counterparty c, CancellationToken _) => c);

        var counterparty = new Counterparty { Type = CounterpartyType.Individual, FullName = "Jane Doe", City = "Москва", Phone = "+1", Email = "jane@example.com" };

        var result = await _sut.CreateAsync(userId, counterparty);

        Assert.Equal(account.Id, result.ClientAccountId);
        _counterpartiesRepository.Verify(
            repository => repository.CreateAsync(
                It.Is<Counterparty>(c => c.ClientAccountId == account.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_NoClientAccountForUser_ReturnsNullWithoutQueryingCounterparties()
    {
        var userId = Guid.NewGuid();
        _clientAccountsRepository
            .Setup(repository => repository.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientAccount?)null);

        var result = await _sut.GetByIdAsync(userId, Guid.NewGuid());

        Assert.Null(result);
        _counterpartiesRepository.Verify(
            repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_ClientAccountExists_DelegatesWithAccountId()
    {
        var userId = Guid.NewGuid();
        var counterpartyId = Guid.NewGuid();
        var account = new ClientAccount { Id = Guid.NewGuid(), UserId = userId };
        var counterparty = new Counterparty { Id = counterpartyId, ClientAccountId = account.Id, City = "Казань", Phone = "+1", Email = "a@example.com" };

        _clientAccountsRepository
            .Setup(repository => repository.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _counterpartiesRepository
            .Setup(repository => repository.GetByIdAsync(counterpartyId, account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(counterparty);

        var result = await _sut.GetByIdAsync(userId, counterpartyId);

        Assert.Same(counterparty, result);
    }

    [Fact]
    public async Task SearchAsync_NoClientAccountForUser_ReturnsEmptyList()
    {
        var userId = Guid.NewGuid();
        _clientAccountsRepository
            .Setup(repository => repository.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientAccount?)null);

        var result = await _sut.SearchAsync(userId, city: null, name: null, phone: null);

        Assert.Empty(result);
        _counterpartiesRepository.Verify(
            repository => repository.SearchAsync(
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_NoClientAccountForUser_ReturnsFalseWithoutUpdating()
    {
        var userId = Guid.NewGuid();
        _clientAccountsRepository
            .Setup(repository => repository.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientAccount?)null);

        var result = await _sut.UpdateAsync(userId, Guid.NewGuid(), new Counterparty());

        Assert.False(result);
        _counterpartiesRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Counterparty>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ClientAccountExists_DelegatesWithAccountId()
    {
        var userId = Guid.NewGuid();
        var counterpartyId = Guid.NewGuid();
        var account = new ClientAccount { Id = Guid.NewGuid(), UserId = userId };

        _clientAccountsRepository
            .Setup(repository => repository.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _counterpartiesRepository
            .Setup(repository => repository.UpdateAsync(counterpartyId, account.Id, It.IsAny<Counterparty>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.UpdateAsync(userId, counterpartyId, new Counterparty());

        Assert.True(result);
    }

    [Fact]
    public async Task DeleteAsync_NoClientAccountForUser_ReturnsFalseWithoutDeleting()
    {
        var userId = Guid.NewGuid();
        _clientAccountsRepository
            .Setup(repository => repository.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientAccount?)null);

        var result = await _sut.DeleteAsync(userId, Guid.NewGuid());

        Assert.False(result);
        _counterpartiesRepository.Verify(
            repository => repository.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_ClientAccountExists_DelegatesWithAccountId()
    {
        var userId = Guid.NewGuid();
        var counterpartyId = Guid.NewGuid();
        var account = new ClientAccount { Id = Guid.NewGuid(), UserId = userId };

        _clientAccountsRepository
            .Setup(repository => repository.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _counterpartiesRepository
            .Setup(repository => repository.DeleteAsync(counterpartyId, account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.DeleteAsync(userId, counterpartyId);

        Assert.True(result);
    }
}
