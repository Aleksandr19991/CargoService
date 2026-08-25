using CargoService.Application.Interfaces;
using CargoService.Application.Models;
using CargoService.Domain.Entities;
using CargoService.Domain.Enums;
using Moq;

namespace CargoService.Application.Tests;

public class ShipmentsServiceTests
{
    private readonly Mock<IShipmentsRepository> _shipmentsRepository = new();
    private readonly Mock<IOutboxWriter> _outboxWriter = new();
    private readonly ShipmentsService _sut;

    public ShipmentsServiceTests()
    {
        _sut = new ShipmentsService(_shipmentsRepository.Object, _outboxWriter.Object);
    }

    private static Shipment NewShipment(
        ShipmentStatus status = ShipmentStatus.Created,
        DateTimeOffset? deadline = null) => new()
    {
        Id = Guid.NewGuid(),
        OrderId = Guid.NewGuid(),
        TrackingNumber = "CS-ABCDEFGHJK",
        CurrentStatus = status,
        CreatedAt = DateTimeOffset.UtcNow,
        DeliveryDeadline = deadline,
    };

    private static ShipmentAcceptance NewAcceptance(
        PackagingCondition packaging = PackagingCondition.Intact,
        IReadOnlyCollection<Guid>? photos = null,
        IReadOnlyCollection<PackagingType>? packagingTypes = null) => new()
    {
        InspectedByUserId = Guid.NewGuid(),
        PackagingCondition = packaging,
        CargoCondition = CargoCondition.Intact,
        Comment = "комментарий",
        PhotoFileIds = photos ?? [],
        PerformedPackagingTypes = packagingTypes ?? [],
        Location = "Москва",
    };

    private void SetupGet(Shipment shipment) =>
        _shipmentsRepository
            .Setup(repository => repository.GetByIdWithDetailsAsync(shipment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

    private void VerifyEnqueued(string routingKey, Times times) =>
        _outboxWriter.Verify(
            writer => writer.Enqueue(It.IsAny<Guid>(), routingKey, It.IsAny<string>(), It.IsAny<DateTimeOffset>()),
            times);

    // ---------- Создание груза по OrderConfirmed ----------

    [Fact]
    public async Task CreateFromConfirmedOrder_GeneratesTrackingNumberAndSeedsHistory()
    {
        var orderId = Guid.NewGuid();
        var deadline = DateTimeOffset.UtcNow.AddDays(3);
        _shipmentsRepository
            .Setup(repository => repository.ExistsByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _shipmentsRepository
            .Setup(repository => repository.TryCreateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shipment shipment, CancellationToken _) => shipment);

        var result = await _sut.CreateFromConfirmedOrderAsync(orderId, deadline);

        Assert.NotNull(result);
        Assert.Equal(orderId, result!.OrderId);
        Assert.Equal(ShipmentStatus.Created, result.CurrentStatus);
        Assert.Equal(deadline, result.DeliveryDeadline);
        Assert.Matches(@"^CS-[A-HJ-NP-Z2-9]{10}$", result.TrackingNumber);

        // Хронология должна быть непустой с самого начала, иначе трекинг покажет статус без истории.
        var history = Assert.Single(result.StatusHistory);
        Assert.Equal(ShipmentStatus.Created, history.Status);
    }

    [Fact]
    public async Task CreateFromConfirmedOrder_AlreadyExists_ReturnsNullWithoutInserting()
    {
        var orderId = Guid.NewGuid();
        _shipmentsRepository
            .Setup(repository => repository.ExistsByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.CreateFromConfirmedOrderAsync(orderId, deliveryDeadline: null);

        Assert.Null(result);
        _shipmentsRepository.Verify(
            repository => repository.TryCreateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------- Приёмка ----------

    [Fact]
    public async Task Accept_CreatedShipment_RecordsActPackagingHistoryAndPublishesThreeEvents()
    {
        var shipment = NewShipment();
        SetupGet(shipment);
        var acceptance = NewAcceptance(
            photos: [Guid.NewGuid()],
            packagingTypes: [PackagingType.Pallet, PackagingType.Special]);

        var result = await _sut.AcceptAsync(shipment.Id, acceptance);

        Assert.Equal(ShipmentOperationResult.Success, result);
        Assert.Equal(ShipmentStatus.Accepted, shipment.CurrentStatus);
        Assert.Single(shipment.Inspections);
        Assert.Equal(2, shipment.PackagingServices.Count);
        Assert.Single(shipment.StatusHistory, history => history.Status == ShipmentStatus.Accepted);

        VerifyEnqueued("cargo-service.cargo-accepted", Times.Once());
        VerifyEnqueued("cargo-service.cargo-status-changed", Times.Once());
        VerifyEnqueued("cargo-service.cargo-photo-uploaded", Times.Once());
    }

    [Fact]
    public async Task Accept_WithoutPhotos_DoesNotPublishPhotoEvent()
    {
        var shipment = NewShipment();
        SetupGet(shipment);

        await _sut.AcceptAsync(shipment.Id, NewAcceptance());

        VerifyEnqueued("cargo-service.cargo-photo-uploaded", Times.Never());
    }

    [Theory]
    [InlineData(ShipmentStatus.Accepted)]
    [InlineData(ShipmentStatus.InTransit)]
    [InlineData(ShipmentStatus.Delivered)]
    public async Task Accept_NotInCreatedStatus_ReturnsConflict(ShipmentStatus status)
    {
        var shipment = NewShipment(status);
        SetupGet(shipment);

        var result = await _sut.AcceptAsync(shipment.Id, NewAcceptance());

        Assert.Equal(ShipmentOperationResult.Conflict, result);
        _shipmentsRepository.Verify(
            repository => repository.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Accept_UnknownShipment_ReturnsNotFound()
    {
        _shipmentsRepository
            .Setup(repository => repository.GetByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shipment?)null);

        var result = await _sut.AcceptAsync(Guid.NewGuid(), NewAcceptance());

        Assert.Equal(ShipmentOperationResult.NotFound, result);
    }

    // ---------- Фото ----------

    [Fact]
    public async Task AddPhotos_OnlyNewFilesAreStoredAndPublished()
    {
        var existing = Guid.NewGuid();
        var added = Guid.NewGuid();
        var shipment = NewShipment(ShipmentStatus.Accepted);
        shipment.Inspections.Add(new AcceptanceInspection { PhotoFileIds = [existing], InspectedAt = DateTimeOffset.UtcNow });
        SetupGet(shipment);

        var result = await _sut.AddPhotosAsync(shipment.Id, [existing, added]);

        Assert.Equal(ShipmentOperationResult.Success, result);
        Assert.Equal([existing, added], shipment.Inspections.Single().PhotoFileIds);
        VerifyEnqueued("cargo-service.cargo-photo-uploaded", Times.Once());
    }

    [Fact]
    public async Task AddPhotos_AllDuplicates_PublishesNothingAndSkipsSave()
    {
        var existing = Guid.NewGuid();
        var shipment = NewShipment(ShipmentStatus.Accepted);
        shipment.Inspections.Add(new AcceptanceInspection { PhotoFileIds = [existing], InspectedAt = DateTimeOffset.UtcNow });
        SetupGet(shipment);

        var result = await _sut.AddPhotosAsync(shipment.Id, [existing]);

        Assert.Equal(ShipmentOperationResult.Success, result);
        VerifyEnqueued("cargo-service.cargo-photo-uploaded", Times.Never());
        _shipmentsRepository.Verify(
            repository => repository.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddPhotos_ShipmentNotAccepted_ReturnsConflict()
    {
        var shipment = NewShipment();
        SetupGet(shipment);

        var result = await _sut.AddPhotosAsync(shipment.Id, [Guid.NewGuid()]);

        Assert.Equal(ShipmentOperationResult.Conflict, result);
    }

    // ---------- Смена статуса ----------

    [Fact]
    public async Task ChangeStatus_AppendsHistoryAndPublishesEvent()
    {
        var shipment = NewShipment(ShipmentStatus.Accepted);
        SetupGet(shipment);

        var result = await _sut.ChangeStatusAsync(shipment.Id, new ShipmentStatusChange
        {
            Status = ShipmentStatus.InTransit,
            Location = "Москва",
        });

        Assert.Equal(ShipmentOperationResult.Success, result);
        Assert.Equal(ShipmentStatus.InTransit, shipment.CurrentStatus);
        Assert.Single(shipment.StatusHistory);
        VerifyEnqueued("cargo-service.cargo-status-changed", Times.Once());
        VerifyEnqueued("cargo-service.cargo-delivered", Times.Never());
    }

    [Fact]
    public async Task ChangeStatus_SameStatusAgain_IsAllowedAndAppendsAnotherHistoryEntry()
    {
        // Несколько отметок «ВПути» с разными городами — это трекинг, а не дубликат.
        var shipment = NewShipment(ShipmentStatus.InTransit);
        SetupGet(shipment);

        var first = await _sut.ChangeStatusAsync(shipment.Id, new ShipmentStatusChange { Status = ShipmentStatus.InTransit, Location = "Москва" });
        var second = await _sut.ChangeStatusAsync(shipment.Id, new ShipmentStatusChange { Status = ShipmentStatus.InTransit, Location = "Владимир" });

        Assert.Equal(ShipmentOperationResult.Success, first);
        Assert.Equal(ShipmentOperationResult.Success, second);
        Assert.Equal(2, shipment.StatusHistory.Count);
        VerifyEnqueued("cargo-service.cargo-status-changed", Times.Exactly(2));
    }

    [Fact]
    public async Task ChangeStatus_ToDelivered_AlsoPublishesCargoDelivered()
    {
        var shipment = NewShipment(ShipmentStatus.InTransit);
        SetupGet(shipment);

        await _sut.ChangeStatusAsync(shipment.Id, new ShipmentStatusChange { Status = ShipmentStatus.Delivered });

        VerifyEnqueued("cargo-service.cargo-status-changed", Times.Once());
        VerifyEnqueued("cargo-service.cargo-delivered", Times.Once());
    }

    [Fact]
    public async Task ChangeStatus_FromDelivered_ReturnsConflict()
    {
        var shipment = NewShipment(ShipmentStatus.Delivered);
        SetupGet(shipment);

        var result = await _sut.ChangeStatusAsync(shipment.Id, new ShipmentStatusChange { Status = ShipmentStatus.InTransit });

        Assert.Equal(ShipmentOperationResult.Conflict, result);
    }

    // ---------- Публичный трекинг ----------

    [Theory]
    [InlineData("cs-abcdefghjk")]
    [InlineData("  CS-ABCDEFGHJK  ")]
    public async Task GetByTrackingNumber_NormalisesInput(string typedByUser)
    {
        var shipment = NewShipment();
        _shipmentsRepository
            .Setup(repository => repository.GetByTrackingNumberAsync("CS-ABCDEFGHJK", It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        var result = await _sut.GetByTrackingNumberAsync(typedByUser);

        Assert.Same(shipment, result);
    }

    // ---------- Вердикт ИИ ----------

    [Theory]
    // Сотрудник сказал «целая», ИИ уверенно видит повреждение — расхождение.
    [InlineData(PackagingCondition.Intact, true, 0.95, true)]
    // Оценки совпали.
    [InlineData(PackagingCondition.Damaged, true, 0.90, false)]
    // Ниже порога уверенности — не расхождение, иначе флагу перестанут верить.
    [InlineData(PackagingCondition.Intact, true, 0.55, false)]
    // Обратное расхождение: сотрудник видит повреждение, ИИ уверенно — нет.
    [InlineData(PackagingCondition.Damaged, false, 0.95, true)]
    public async Task ApplyIntegrityAssessment_SetsDiscrepancyFlag(
        PackagingCondition humanVerdict,
        bool aiDamageDetected,
        double confidence,
        bool expectedDiscrepancy)
    {
        var shipment = NewShipment(ShipmentStatus.Accepted);
        shipment.Inspections.Add(new AcceptanceInspection
        {
            PackagingCondition = humanVerdict,
            InspectedAt = DateTimeOffset.UtcNow,
        });
        SetupGet(shipment);

        var applied = await _sut.ApplyIntegrityAssessmentAsync(shipment.Id, new PackageIntegrityAssessment
        {
            InspectionJobId = Guid.NewGuid(),
            DamageDetected = aiDamageDetected,
            Confidence = confidence,
        });

        var inspection = shipment.Inspections.Single();
        Assert.True(applied);
        Assert.Equal(expectedDiscrepancy, inspection.HasAssessmentDiscrepancy);
        Assert.Equal(aiDamageDetected, inspection.AiDamageDetected);
        Assert.Equal(confidence, inspection.AiConfidence);
    }

    [Fact]
    public async Task ApplyIntegrityAssessment_ShipmentWithoutAcceptanceAct_ReturnsFalse()
    {
        var shipment = NewShipment();
        SetupGet(shipment);

        var applied = await _sut.ApplyIntegrityAssessmentAsync(shipment.Id, new PackageIntegrityAssessment
        {
            InspectionJobId = Guid.NewGuid(),
            DamageDetected = true,
            Confidence = 0.99,
        });

        Assert.False(applied);
    }

    // ---------- Контроль SLA ----------

    [Fact]
    public async Task FlagOverdue_MarksShipmentsDelayedWithHistoryAndEvents()
    {
        var overdue = new List<Shipment>
        {
            NewShipment(ShipmentStatus.InTransit, DateTimeOffset.UtcNow.AddDays(-1)),
            NewShipment(ShipmentStatus.InWarehouse, DateTimeOffset.UtcNow.AddDays(-2)),
        };
        _shipmentsRepository
            .Setup(repository => repository.GetOverdueAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<IReadOnlyCollection<ShipmentStatus>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(overdue);

        var flagged = await _sut.FlagOverdueShipmentsAsync(batchSize: 100);

        Assert.Equal(2, flagged);
        Assert.All(overdue, shipment =>
        {
            Assert.Equal(ShipmentStatus.Delayed, shipment.CurrentStatus);
            Assert.Single(shipment.StatusHistory);
        });
        VerifyEnqueued("cargo-service.cargo-status-changed", Times.Exactly(2));
        _shipmentsRepository.Verify(
            repository => repository.UpdateRangeAsync(overdue, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FlagOverdue_ExcludesStatusesWhereDelayIsMeaningless()
    {
        _shipmentsRepository
            .Setup(repository => repository.GetOverdueAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<IReadOnlyCollection<ShipmentStatus>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var flagged = await _sut.FlagOverdueShipmentsAsync(batchSize: 100);

        Assert.Equal(0, flagged);

        // Набор статусов — часть контракта джобы: уже помеченные, доставленные, проблемные и
        // ждущие получателя грузы помечать повторно/ошибочно нельзя.
        _shipmentsRepository.Verify(
            repository => repository.GetOverdueAsync(
                It.IsAny<DateTimeOffset>(),
                It.Is<IReadOnlyCollection<ShipmentStatus>>(statuses =>
                    !statuses.Contains(ShipmentStatus.Delayed)
                    && !statuses.Contains(ShipmentStatus.Delivered)
                    && !statuses.Contains(ShipmentStatus.Problem)
                    && !statuses.Contains(ShipmentStatus.ReadyForPickup)),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
