using DocumentService.Application.Interfaces;
using DocumentService.Application.Models;
using DocumentService.Domain.Entities;
using DocumentService.Domain.Enums;
using Moq;

namespace DocumentService.Application.Tests;

public class DocumentsServiceTests
{
    private readonly Mock<IDocumentsRepository> repository = new();
    private readonly List<Document> saved = [];

    public DocumentsServiceTests()
    {
        repository
            .Setup(instance => instance.AddRangeAsync(It.IsAny<IReadOnlyList<Document>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<Document>, CancellationToken>((documents, _) => saved.AddRange(documents))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task RegisterForAcceptanceAsync_IssuesWaybillAndAcceptanceActForIntactCargo()
    {
        var facts = Acceptance("Intact", "Intact");

        var documents = await CreateService().RegisterForAcceptanceAsync(facts, CancellationToken.None);

        Assert.Equal([DocumentType.Waybill, DocumentType.AcceptanceAct], documents.Select(document => document.Type));
        Assert.Equal(documents, saved);
        Assert.All(documents, document =>
        {
            Assert.Equal(DocumentStatus.Pending, document.Status);
            Assert.Equal(facts.ShipmentId, document.ShipmentId);
            Assert.Equal(facts.AcceptedAt, document.IssuedAt);
            Assert.Equal("Intact", document.PackagingCondition);
        });
    }

    [Theory]
    [InlineData("Damaged", "Intact")]
    [InlineData("Intact", "Damaged")]
    // Значение, которого сервис ещё не знает: cargo-service обещает расширять перечисления,
    // и лишний акт осмотра лучше пропущенного.
    [InlineData("Intact", "PartiallyDamaged")]
    public async Task RegisterForAcceptanceAsync_AddsDamageActWhenSomethingIsNotIntact(
        string packaging,
        string cargo)
    {
        var documents = await CreateService()
            .RegisterForAcceptanceAsync(Acceptance(packaging, cargo), CancellationToken.None);

        Assert.Contains(documents, document => document.Type == DocumentType.DamageInspectionAct);
        Assert.Equal(3, documents.Count);
    }

    [Fact]
    public async Task RegisterForAcceptanceAsync_IsCaseInsensitiveAboutIntact()
    {
        var documents = await CreateService()
            .RegisterForAcceptanceAsync(Acceptance("intact", "INTACT"), CancellationToken.None);

        Assert.DoesNotContain(documents, document => document.Type == DocumentType.DamageInspectionAct);
    }

    [Fact]
    public async Task RegisterForDeliveryAsync_IssuesSecondAcceptanceActWithRecipient()
    {
        var facts = new CargoDeliveryFacts
        {
            ShipmentId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            TrackingNumber = "CS-TEST000001",
            DeliveredAt = DateTimeOffset.UtcNow,
            ReceivedByName = "Сидоров И. П.",
        };

        var documents = await CreateService().RegisterForDeliveryAsync(facts, CancellationToken.None);

        var document = Assert.Single(documents);
        Assert.Equal(DocumentType.AcceptanceAct, document.Type);
        Assert.Equal("Сидоров И. П.", document.ReceivedByName);

        // Состояний груза на выдаче нет — их там никто не фиксировал.
        Assert.Null(document.PackagingCondition);
        Assert.Null(document.CargoCondition);
    }

    private DocumentsService CreateService() => new(repository.Object);

    private static CargoAcceptanceFacts Acceptance(string packaging, string cargo) => new()
    {
        ShipmentId = Guid.NewGuid(),
        OrderId = Guid.NewGuid(),
        TrackingNumber = "CS-TEST000001",
        AcceptedAt = DateTimeOffset.UtcNow,
        PackagingCondition = packaging,
        CargoCondition = cargo,
    };
}
