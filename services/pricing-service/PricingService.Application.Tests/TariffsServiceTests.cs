using Moq;
using PricingService.Application.Interfaces;
using PricingService.Domain.Entities;
using PricingService.Domain.Enums;

namespace PricingService.Application.Tests;

public class TariffsServiceTests
{
    private readonly Mock<ITariffRatesRepository> _tariffRatesRepository = new();
    private readonly Mock<IOutboxWriter> _outboxWriter = new();
    private readonly TariffsService _sut;

    public TariffsServiceTests()
    {
        _sut = new TariffsService(_tariffRatesRepository.Object, _outboxWriter.Object);
    }

    [Fact]
    public async Task GetAllCurrentAsync_DelegatesToRepository()
    {
        var rates = new List<TariffRate> { new() { Id = Guid.NewGuid() } };
        _tariffRatesRepository.Setup(repository => repository.GetAllCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rates);

        var result = await _sut.GetAllCurrentAsync();

        Assert.Same(rates, result);
    }

    [Fact]
    public async Task UpdatePriceAsync_UnknownId_ReturnsNullWithoutEnqueueingOrReplacing()
    {
        _tariffRatesRepository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TariffRate?)null);

        var result = await _sut.UpdatePriceAsync(Guid.NewGuid(), 999m);

        Assert.Null(result);
        _outboxWriter.Verify(
            writer => writer.Enqueue(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()),
            Times.Never);
        _tariffRatesRepository.Verify(
            repository => repository.ReplaceAsync(
                It.IsAny<TariffRate>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdatePriceAsync_KnownId_EnqueuesTariffChangedBeforeReplacing()
    {
        var id = Guid.NewGuid();
        var current = new TariffRate
        {
            Id = id,
            Category = TariffCategory.ShippingType,
            Code = "Express",
            Name = "Экспресс",
            Price = 500m,
            PriceType = TariffPriceType.Fixed,
            ValidFrom = DateTimeOffset.UtcNow,
        };

        _tariffRatesRepository
            .Setup(repository => repository.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);

        TariffRate? replacement = null;
        _tariffRatesRepository
            .Setup(repository => repository.ReplaceAsync(current, It.IsAny<Guid>(), 600m, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TariffRate c, Guid newId, decimal newPrice, DateTimeOffset now, CancellationToken _) =>
            {
                replacement = new TariffRate { Id = newId, Category = c.Category, Code = c.Code, Name = c.Name, Price = newPrice, PriceType = c.PriceType, ValidFrom = now };
                return replacement;
            });

        var result = await _sut.UpdatePriceAsync(id, 600m);

        Assert.NotNull(result);
        Assert.Equal(600m, result!.Price);
        _outboxWriter.Verify(
            writer => writer.Enqueue(
                It.IsAny<Guid>(),
                "pricing-service.tariff-changed",
                It.Is<string>(json => json.Contains("\"Code\":\"Express\"") && json.Contains("\"Price\":600")),
                It.IsAny<DateTimeOffset>()),
            Times.Once);
    }
}
