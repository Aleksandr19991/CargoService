using DocumentService.Application.Interfaces;
using DocumentService.Domain.Entities;
using DocumentService.Domain.Enums;
using Moq;

namespace DocumentService.Application.Tests;

public class DocumentsQueryServiceTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid StrangerId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid ShipmentId = Guid.NewGuid();

    private readonly Mock<IDocumentsRepository> documents = new();
    private readonly Mock<IOrderSnapshotsRepository> snapshots = new();

    public DocumentsQueryServiceTests()
    {
        documents
            .Setup(repository => repository.GetByOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Document()]);

        documents
            .Setup(repository => repository.GetByShipmentAsync(ShipmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Document()]);

        snapshots
            .Setup(repository => repository.GetAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderSnapshot { OrderId = OrderId, ClientAccountId = OwnerId });
    }

    [Fact]
    public async Task GetByOrderAsync_ReturnsDocumentsToOwner()
    {
        var found = await CreateService().GetByOrderAsync(OrderId, DocumentAccess.Client(OwnerId), CancellationToken.None);

        Assert.Single(found);
    }

    [Fact]
    public async Task GetByOrderAsync_HidesDocumentsFromOtherClients()
    {
        // Пустой список, а не отказ: иначе по коду ответа можно было бы выяснять, существует ли
        // заявка с таким идентификатором.
        var found = await CreateService().GetByOrderAsync(OrderId, DocumentAccess.Client(StrangerId), CancellationToken.None);

        Assert.Empty(found);
    }

    [Fact]
    public async Task GetByOrderAsync_ShowsAnyDocumentsToStaff()
    {
        var found = await CreateService().GetByOrderAsync(OrderId, DocumentAccess.Staff(StrangerId), CancellationToken.None);

        Assert.Single(found);
    }

    [Fact]
    public async Task GetByOrderAsync_DeniesClientWhenOrderIsUnknown()
    {
        // Сведений о заявке нет — проверить владение нечем, а показать «на всякий случай»
        // значит показать чужое.
        var unknownOrder = Guid.NewGuid();
        documents
            .Setup(repository => repository.GetByOrderAsync(unknownOrder, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Document()]);

        var found = await CreateService().GetByOrderAsync(unknownOrder, DocumentAccess.Client(OwnerId), CancellationToken.None);

        Assert.Empty(found);
    }

    [Fact]
    public async Task GetByShipmentAsync_ChecksOwnershipThroughTheOrder()
    {
        var service = CreateService();

        Assert.Single(await service.GetByShipmentAsync(ShipmentId, DocumentAccess.Client(OwnerId), CancellationToken.None));
        Assert.Empty(await service.GetByShipmentAsync(ShipmentId, DocumentAccess.Client(StrangerId), CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForOtherClients()
    {
        var document = Document();
        documents
            .Setup(repository => repository.GetByIdAsync(document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var service = CreateService();

        Assert.NotNull(await service.GetByIdAsync(document.Id, DocumentAccess.Client(OwnerId), CancellationToken.None));
        Assert.Null(await service.GetByIdAsync(document.Id, DocumentAccess.Client(StrangerId), CancellationToken.None));
    }

    private DocumentsQueryService CreateService() => new(documents.Object, snapshots.Object);

    private static Document Document() => new()
    {
        Id = Guid.NewGuid(),
        Type = DocumentType.Waybill,
        Status = DocumentStatus.Ready,
        ShipmentId = ShipmentId,
        OrderId = OrderId,
        TrackingNumber = "CS-TEST000001",
        IssuedAt = DateTimeOffset.UtcNow,
        CreatedAt = DateTimeOffset.UtcNow,
        FileId = Guid.NewGuid(),
    };
}
