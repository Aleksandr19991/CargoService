using System.Text.RegularExpressions;
using Moq;
using OrdersService.Application.Interfaces;
using OrdersService.Application.Models;
using OrdersService.Domain.Entities;
using OrdersService.Domain.Enums;

namespace OrdersService.Application.Tests;

public class OrdersServiceTests
{
    private readonly Mock<IOrdersRepository> _ordersRepository = new();
    private readonly Mock<IPricingClient> _pricingClient = new();
    private readonly Mock<IOutboxWriter> _outboxWriter = new();
    private readonly OrdersService _sut;

    public OrdersServiceTests()
    {
        _sut = new OrdersService(_ordersRepository.Object, _pricingClient.Object, _outboxWriter.Object);
    }

    private static Order NewOrder(Guid? id = null, OrderStatus status = OrderStatus.Created, decimal? calculatedPrice = 100m) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Number = "20260101-ABCDEF",
        Status = status,
        OriginCity = "Moscow",
        DestinationCity = "Kazan",
        CargoName = "Electronics",
        CalculatedPrice = calculatedPrice,
    };

    [Fact]
    public async Task CreateAsync_AssignsFieldsCalculatesPriceAndEnqueuesOrderCreatedBeforePersisting()
    {
        var userId = Guid.NewGuid();
        var order = new Order { OriginCity = "Moscow", DestinationCity = "Kazan", CargoName = "Electronics" };

        _pricingClient
            .Setup(client => client.CalculateAsync(It.IsAny<PriceCalculationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PriceCalculationResult { TotalPrice = 500m, Breakdown = [] });

        _ordersRepository
            .Setup(repository => repository.CreateAsync(order, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var result = await _sut.CreateAsync(userId, order);

        Assert.Same(order, result);
        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.Equal(userId, order.ClientAccountId);
        Assert.Equal(OrderStatus.Created, order.Status);
        Assert.Equal(500m, order.CalculatedPrice);
        Assert.Matches(new Regex(@"^\d{8}-[A-Z0-9]{6}$"), order.Number);

        _outboxWriter.Verify(
            writer => writer.Enqueue(It.IsAny<Guid>(), "orders-service.order-created", It.IsAny<string>(), It.IsAny<DateTimeOffset>()),
            Times.Once);
        _ordersRepository.Verify(repository => repository.CreateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_DelegatesToRepositoryWithOwnership()
    {
        var userId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var order = NewOrder(id);
        _ordersRepository.Setup(repository => repository.GetByIdAsync(id, userId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var result = await _sut.GetByIdAsync(userId, id);

        Assert.Same(order, result);
    }

    [Fact]
    public async Task GetByClientAsync_DelegatesToRepository()
    {
        var userId = Guid.NewGuid();
        var items = new List<Order> { NewOrder() };
        _ordersRepository
            .Setup(repository => repository.GetByClientAsync(userId, OrderStatus.Confirmed, 2, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 1));

        var (resultItems, totalCount) = await _sut.GetByClientAsync(userId, OrderStatus.Confirmed, 2, 10);

        Assert.Same(items, resultItems);
        Assert.Equal(1, totalCount);
    }

    [Fact]
    public async Task ConfirmAsync_CreatedOrder_TransitionsToConfirmedAndEnqueuesEvent()
    {
        var userId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var order = NewOrder(id, OrderStatus.Created);
        _ordersRepository.Setup(repository => repository.GetByIdAsync(id, userId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var result = await _sut.ConfirmAsync(userId, id);

        Assert.Equal(OrderTransitionResult.Success, result);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        _outboxWriter.Verify(
            writer => writer.Enqueue(It.IsAny<Guid>(), "orders-service.order-confirmed", It.IsAny<string>(), It.IsAny<DateTimeOffset>()),
            Times.Once);
        _ordersRepository.Verify(repository => repository.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmAsync_AlreadyConfirmed_ReturnsNoChangeWithoutEnqueueingOrUpdating()
    {
        var userId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var order = NewOrder(id, OrderStatus.Confirmed);
        _ordersRepository.Setup(repository => repository.GetByIdAsync(id, userId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var result = await _sut.ConfirmAsync(userId, id);

        Assert.Equal(OrderTransitionResult.NoChange, result);
        _outboxWriter.Verify(
            writer => writer.Enqueue(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()),
            Times.Never);
        _ordersRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmAsync_CancelledOrder_ReturnsConflict()
    {
        var userId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var order = NewOrder(id, OrderStatus.Cancelled);
        _ordersRepository.Setup(repository => repository.GetByIdAsync(id, userId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var result = await _sut.ConfirmAsync(userId, id);

        Assert.Equal(OrderTransitionResult.Conflict, result);
        _ordersRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmAsync_UnknownId_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var id = Guid.NewGuid();
        _ordersRepository.Setup(repository => repository.GetByIdAsync(id, userId, It.IsAny<CancellationToken>())).ReturnsAsync((Order?)null);

        var result = await _sut.ConfirmAsync(userId, id);

        Assert.Equal(OrderTransitionResult.NotFound, result);
    }

    [Theory]
    [InlineData(OrderStatus.Created)]
    [InlineData(OrderStatus.Confirmed)]
    public async Task CancelAsync_CreatedOrConfirmedOrder_TransitionsToCancelledAndEnqueuesEvent(OrderStatus fromStatus)
    {
        var userId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var order = NewOrder(id, fromStatus);
        _ordersRepository.Setup(repository => repository.GetByIdAsync(id, userId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var result = await _sut.CancelAsync(userId, id);

        Assert.Equal(OrderTransitionResult.Success, result);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        _outboxWriter.Verify(
            writer => writer.Enqueue(It.IsAny<Guid>(), "orders-service.order-cancelled", It.IsAny<string>(), It.IsAny<DateTimeOffset>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelAsync_AlreadyCancelled_ReturnsNoChangeWithoutEnqueueingOrUpdating()
    {
        var userId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var order = NewOrder(id, OrderStatus.Cancelled);
        _ordersRepository.Setup(repository => repository.GetByIdAsync(id, userId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var result = await _sut.CancelAsync(userId, id);

        Assert.Equal(OrderTransitionResult.NoChange, result);
        _outboxWriter.Verify(
            writer => writer.Enqueue(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()),
            Times.Never);
        _ordersRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelAsync_UnknownId_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var id = Guid.NewGuid();
        _ordersRepository.Setup(repository => repository.GetByIdAsync(id, userId, It.IsAny<CancellationToken>())).ReturnsAsync((Order?)null);

        var result = await _sut.CancelAsync(userId, id);

        Assert.Equal(OrderTransitionResult.NotFound, result);
    }

    [Fact]
    public async Task UpdateCargoStatusAsync_KnownOrder_SetsFieldsAndReturnsTrue()
    {
        var id = Guid.NewGuid();
        var order = NewOrder(id);
        _ordersRepository.Setup(repository => repository.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var result = await _sut.UpdateCargoStatusAsync(id, "TRACK-1", "InTransit");

        Assert.True(result);
        Assert.Equal("TRACK-1", order.TrackingNumber);
        Assert.Equal("InTransit", order.CargoStatus);
        _ordersRepository.Verify(repository => repository.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCargoStatusAsync_UnknownOrder_ReturnsFalseWithoutUpdating()
    {
        var id = Guid.NewGuid();
        _ordersRepository.Setup(repository => repository.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Order?)null);

        var result = await _sut.UpdateCargoStatusAsync(id, "TRACK-1", "InTransit");

        Assert.False(result);
        _ordersRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarkPaidAsync_KnownOrder_SetsFieldsAndReturnsTrue()
    {
        var id = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var order = NewOrder(id);
        _ordersRepository.Setup(repository => repository.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var result = await _sut.MarkPaidAsync(id, paymentId);

        Assert.True(result);
        Assert.True(order.IsPaid);
        Assert.Equal(paymentId, order.PaymentId);
        _ordersRepository.Verify(repository => repository.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkPaidAsync_UnknownOrder_ReturnsFalseWithoutUpdating()
    {
        var id = Guid.NewGuid();
        _ordersRepository.Setup(repository => repository.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Order?)null);

        var result = await _sut.MarkPaidAsync(id, Guid.NewGuid());

        Assert.False(result);
        _ordersRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
