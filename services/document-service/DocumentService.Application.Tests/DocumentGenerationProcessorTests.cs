using System.Text.Json;
using CargoService.Contracts.Events.V1;
using DocumentService.Application.Interfaces;
using DocumentService.Application.Models;
using DocumentService.Domain.Entities;
using DocumentService.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DocumentService.Application.Tests;

public class DocumentGenerationProcessorTests
{
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46];

    private readonly Mock<IDocumentsRepository> documents = new();
    private readonly Mock<IOrderSnapshotsRepository> snapshots = new();
    private readonly Mock<IDocumentRenderer> renderer = new();
    private readonly Mock<IFileStorageClient> storage = new();
    private readonly Mock<IOutboxWriter> outbox = new();

    private readonly Guid fileId = Guid.NewGuid();

    public DocumentGenerationProcessorTests()
    {
        renderer.Setup(instance => instance.RenderWaybill(It.IsAny<WaybillModel>())).Returns(Pdf);
        renderer.Setup(instance => instance.RenderAcceptanceAct(It.IsAny<AcceptanceActModel>())).Returns(Pdf);
        renderer.Setup(instance => instance.RenderDamageInspectionAct(It.IsAny<DamageInspectionActModel>())).Returns(Pdf);

        storage
            .Setup(instance => instance.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileId);
    }

    [Fact]
    public async Task ProcessNextAsync_ReturnsFalseWhenNothingToPrint()
    {
        Claim(null);

        Assert.False(await CreateProcessor().ProcessNextAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ProcessNextAsync_PrintsWaybillFromOrderSnapshot()
    {
        var document = Claim(Document(DocumentType.Waybill));
        WithSnapshot(document.OrderId);

        WaybillModel? printed = null;
        renderer
            .Setup(instance => instance.RenderWaybill(It.IsAny<WaybillModel>()))
            .Callback<WaybillModel>(model => printed = model)
            .Returns(Pdf);

        Assert.True(await CreateProcessor().ProcessNextAsync(CancellationToken.None));

        Assert.NotNull(printed);
        Assert.Equal("20260830-TEST01", printed.OrderNumber);
        Assert.Equal("ООО «Отправитель»", printed.SenderName);
        Assert.Equal(18450.50m, printed.Price);
        Assert.Equal(document.TrackingNumber, printed.TrackingNumber);

        documents.Verify(
            repository => repository.MarkReadyAsync(document.Id, fileId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessNextAsync_PublishesDocumentGeneratedThroughOutbox()
    {
        var document = Claim(Document(DocumentType.Waybill));
        WithSnapshot(document.OrderId);

        string? routingKey = null;
        DocumentGenerated? published = null;
        outbox
            .Setup(writer => writer.Enqueue(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Callback<Guid, string, string, DateTimeOffset>((_, key, payload, _) =>
            {
                routingKey = key;
                published = JsonSerializer.Deserialize<DocumentGenerated>(payload);
            });

        await CreateProcessor().ProcessNextAsync(CancellationToken.None);

        Assert.Equal("document-service.document-generated", routingKey);
        Assert.NotNull(published);
        Assert.Equal(document.ShipmentId, published.ShipmentId);
        Assert.Equal(document.OrderId, published.OrderId);
        Assert.Equal("Waybill", published.DocumentType);
        Assert.Equal(fileId, published.DocumentFileId);
    }

    [Fact]
    public async Task ProcessNextAsync_PrintsWithBlanksWhenOrderSnapshotIsMissing()
    {
        // События груза и заявки идут разными очередями: снимка может не быть, и документ всё
        // равно печатается — пустые поля видны прочерком, ненапечатанный документ не виден.
        var document = Claim(Document(DocumentType.Waybill));
        snapshots
            .Setup(repository => repository.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderSnapshot?)null);

        WaybillModel? printed = null;
        renderer
            .Setup(instance => instance.RenderWaybill(It.IsAny<WaybillModel>()))
            .Callback<WaybillModel>(model => printed = model)
            .Returns(Pdf);

        Assert.True(await CreateProcessor().ProcessNextAsync(CancellationToken.None));

        Assert.NotNull(printed);
        Assert.Null(printed.OrderNumber);
        documents.Verify(
            repository => repository.MarkReadyAsync(document.Id, fileId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessNextAsync_DetectsHandoverStageFromDocumentData()
    {
        var delivery = Document(DocumentType.AcceptanceAct);
        delivery.PackagingCondition = null;
        delivery.CargoCondition = null;
        delivery.ReceivedByName = "Сидоров И. П.";
        Claim(delivery);
        WithSnapshot(delivery.OrderId);

        AcceptanceActModel? printed = null;
        renderer
            .Setup(instance => instance.RenderAcceptanceAct(It.IsAny<AcceptanceActModel>()))
            .Callback<AcceptanceActModel>(model => printed = model)
            .Returns(Pdf);

        await CreateProcessor().ProcessNextAsync(CancellationToken.None);

        Assert.NotNull(printed);
        Assert.Equal(HandoverStage.Delivery, printed.Stage);
        Assert.Equal("Сидоров И. П.", printed.ReceivedByName);
    }

    [Fact]
    public async Task ProcessNextAsync_FailsDocumentWhenStorageRejectsIt()
    {
        var document = Claim(Document(DocumentType.Waybill));
        WithSnapshot(document.OrderId);
        storage
            .Setup(instance => instance.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("storage is down"));

        string? reason = null;
        documents
            .Setup(repository => repository.MarkFailedAsync(document.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, CancellationToken>((_, value, _) => reason = value)
            .Returns(Task.CompletedTask);

        // Сбой закрывает документ, а не роняет цикл: остальные документы должны печататься.
        Assert.True(await CreateProcessor().ProcessNextAsync(CancellationToken.None));
        Assert.Equal("storage is down", reason);

        outbox.Verify(
            writer => writer.Enqueue(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()),
            Times.Never);
        documents.Verify(
            repository => repository.MarkReadyAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private DocumentGenerationProcessor CreateProcessor() =>
        new(documents.Object,
            snapshots.Object,
            renderer.Object,
            storage.Object,
            outbox.Object,
            NullLogger<DocumentGenerationProcessor>.Instance);

    private Document Claim(Document? document)
    {
        documents
            .Setup(repository => repository.ClaimNextPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        return document!;
    }

    private void WithSnapshot(Guid orderId) =>
        snapshots
            .Setup(repository => repository.GetAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderSnapshot
            {
                OrderId = orderId,
                OrderNumber = "20260830-TEST01",
                OriginCity = "Москва",
                DestinationCity = "Владимир",
                SenderName = "ООО «Отправитель»",
                RecipientName = "Сидоров Иван Петрович",
                CargoName = "Шкаф-купе",
                CargoWeightKg = 148.5m,
                CargoVolumeM3 = 1.24m,
                DeclaredValue = 250000m,
                CalculatedPrice = 18450.50m,
            });

    private static Document Document(DocumentType type) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        Status = DocumentStatus.Processing,
        ShipmentId = Guid.NewGuid(),
        OrderId = Guid.NewGuid(),
        TrackingNumber = "CS-TEST000001",
        IssuedAt = DateTimeOffset.UtcNow,
        PackagingCondition = "Intact",
        CargoCondition = "Intact",
        CreatedAt = DateTimeOffset.UtcNow,
    };
}
