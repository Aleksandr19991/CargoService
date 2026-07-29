using Moq;
using PricingService.Application.Interfaces;
using PricingService.Application.Models;
using PricingService.Domain.Entities;
using PricingService.Domain.Enums;

namespace PricingService.Application.Tests;

public class PricingCalculationServiceTests
{
    private readonly Mock<ITariffRatesRepository> _tariffRatesRepository = new();
    private readonly PricingCalculationService _sut;

    public PricingCalculationServiceTests()
    {
        _sut = new PricingCalculationService(_tariffRatesRepository.Object);

        SetUpRate(TariffCategory.BaseRate, TariffCodes.BaseRatePerKg, "Ставка за кг", 50m);
        SetUpRate(TariffCategory.BaseRate, TariffCodes.BaseRatePerKm, "Ставка за км", 15m);
        SetUpRate(TariffCategory.ShippingType, TariffCodes.ShippingStandard, "Обычная", 0m);
        SetUpRate(TariffCategory.ShippingType, TariffCodes.ShippingExpress, "Экспресс", 500m);
        SetUpRate(TariffCategory.PackagingType, TariffCodes.PackagingWooden, "Деревянная упаковка", 300m);
        SetUpRate(TariffCategory.PickupDelivery, TariffCodes.Pickup, "Забор", 400m);
        SetUpRate(TariffCategory.PickupDelivery, TariffCodes.Delivery, "Доставка", 400m);
        SetUpRate(TariffCategory.Insurance, TariffCodes.InsurancePercentage, "Страхование груза", 1.0m, TariffPriceType.Percentage);
    }

    private void SetUpRate(TariffCategory category, string code, string name, decimal price, TariffPriceType priceType = TariffPriceType.Fixed)
    {
        _tariffRatesRepository
            .Setup(repository => repository.GetCurrentAsync(category, code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TariffRate
            {
                Id = Guid.NewGuid(),
                Category = category,
                Code = code,
                Name = name,
                Price = price,
                PriceType = priceType,
                ValidFrom = DateTimeOffset.UtcNow,
            });
    }

    private static PriceCalculationInput BaseInput(
        decimal weightKg = 10,
        decimal volumeM3 = 0.05m,
        decimal distanceKm = 100,
        string shippingTypeCode = "Standard",
        string packagingTypeCode = "Wooden",
        bool needsPickup = false,
        bool needsDelivery = false,
        bool needsInsurance = false,
        decimal? declaredValue = null) => new()
        {
            WeightKg = weightKg,
            VolumeM3 = volumeM3,
            DistanceKm = distanceKm,
            ShippingTypeCode = shippingTypeCode,
            PackagingTypeCode = packagingTypeCode,
            NeedsPickup = needsPickup,
            NeedsDelivery = needsDelivery,
            NeedsInsurance = needsInsurance,
            DeclaredValue = declaredValue,
        };

    [Fact]
    public async Task CalculateAsync_ZeroWeightVolumeDistance_OnlyPackagingSurchargeApplies()
    {
        var result = await _sut.CalculateAsync(BaseInput(weightKg: 0, volumeM3: 0, distanceKm: 0));

        Assert.Equal(300m, result.TotalPrice);
        Assert.Equal(4, result.Breakdown.Count);
        Assert.Equal(0m, result.Breakdown.Single(line => line.Code == "PerKg").Amount);
        Assert.Equal(0m, result.Breakdown.Single(line => line.Code == "PerKm").Amount);
        Assert.Equal(300m, result.Breakdown.Single(line => line.Code == "Wooden").Amount);
    }

    [Fact]
    public async Task CalculateAsync_ActualWeightExceedsVolumetricWeight_BillsActualWeight()
    {
        // volume 0.05 m3 * 200 kg/m3 = 10 kg volumetric — actual weight (50) is higher.
        var result = await _sut.CalculateAsync(BaseInput(weightKg: 50, volumeM3: 0.05m));

        var weightLine = result.Breakdown.Single(line => line.Code == "PerKg");
        Assert.Equal(50m * 50m, weightLine.Amount); // 50 kg * 50/kg
    }

    [Fact]
    public async Task CalculateAsync_VolumetricWeightExceedsActualWeight_BillsVolumetricWeight()
    {
        // volume 1 m3 * 200 kg/m3 = 200 kg volumetric — actual weight (5) is lower.
        var result = await _sut.CalculateAsync(BaseInput(weightKg: 5, volumeM3: 1));

        var weightLine = result.Breakdown.Single(line => line.Code == "PerKg");
        Assert.Equal(200m * 50m, weightLine.Amount); // 200 kg volumetric * 50/kg
    }

    [Fact]
    public async Task CalculateAsync_NoOptionalServicesRequested_ExcludesPickupDeliveryInsuranceLines()
    {
        var result = await _sut.CalculateAsync(BaseInput());

        Assert.DoesNotContain(result.Breakdown, line => line.Code is "Pickup" or "Delivery" or "Percentage");
    }

    [Fact]
    public async Task CalculateAsync_AllOptionalServicesRequested_IncludesAllBreakdownLines()
    {
        var result = await _sut.CalculateAsync(BaseInput(
            shippingTypeCode: "Express",
            needsPickup: true,
            needsDelivery: true,
            needsInsurance: true,
            declaredValue: 10_000m));

        Assert.Contains(result.Breakdown, line => line.Code == "Express");
        Assert.Contains(result.Breakdown, line => line.Code == "Pickup" && line.Amount == 400m);
        Assert.Contains(result.Breakdown, line => line.Code == "Delivery" && line.Amount == 400m);
        Assert.Contains(result.Breakdown, line => line.Code == "Percentage" && line.Amount == 100m); // 1% of 10000
    }

    [Fact]
    public async Task CalculateAsync_InsuranceRequested_ComputesPercentageOfDeclaredValue()
    {
        var result = await _sut.CalculateAsync(BaseInput(needsInsurance: true, declaredValue: 50_000m));

        var insuranceLine = result.Breakdown.Single(line => line.Code == "Percentage");
        Assert.Equal(500m, insuranceLine.Amount); // 1% of 50000
    }

    [Fact]
    public async Task CalculateAsync_TotalPrice_EqualsSumOfBreakdownLines()
    {
        var result = await _sut.CalculateAsync(BaseInput(needsPickup: true));

        Assert.Equal(result.Breakdown.Sum(line => line.Amount), result.TotalPrice);
    }

    [Fact]
    public async Task CalculateAsync_UnknownTariffRate_Throws()
    {
        var input = BaseInput(shippingTypeCode: "UnknownCode");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CalculateAsync(input));
    }
}
