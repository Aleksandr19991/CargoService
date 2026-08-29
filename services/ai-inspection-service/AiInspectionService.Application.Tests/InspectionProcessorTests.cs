using System.Text.Json;
using AiInspectionService.Application.Interfaces;
using AiInspectionService.Application.Models;
using AiInspectionService.Domain.Entities;
using CargoService.Contracts.Events.V1;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AiInspectionService.Application.Tests;

public class InspectionProcessorTests
{
    private static readonly Guid ShipmentId = Guid.NewGuid();

    private readonly Mock<IInspectionJobsRepository> jobs = new();
    private readonly Mock<IFileStorageClient> storage = new();
    private readonly Mock<IOutboxWriter> outbox = new();
    private readonly FakeInspectionModel model = new();

    [Fact]
    public async Task ProcessNextAsync_ReturnsFalseWhenQueueIsEmpty()
    {
        jobs.Setup(repository => repository.ClaimNextQueuedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((InspectionJob?)null);

        var processed = await CreateProcessor().ProcessNextAsync(CancellationToken.None);

        Assert.False(processed);
        Assert.Equal(0, model.InspectCallCount);
    }

    [Fact]
    public async Task ProcessNextAsync_InspectsEveryPhotoAndCompletesJob()
    {
        var job = Claim("a", "b");
        model.WithVerdict("a", damaged: false, confidence: 0.9).WithVerdict("b", damaged: true, confidence: 0.95);
        SetupDownloads("a", "b");

        var written = CaptureResults();
        var processed = await CreateProcessor().ProcessNextAsync(CancellationToken.None);

        Assert.True(processed);
        Assert.Equal(2, model.InspectCallCount);
        Assert.Equal(2, written.Count);
        Assert.Equal(job.PhotoFileIds, written.Select(result => result.FileId).ToList());
        Assert.Equal("fake-1.0", written[0].ModelVersion);
        Assert.False(written[0].DamageDetected);
        Assert.True(written[1].DamageDetected);
    }

    [Fact]
    public async Task ProcessNextAsync_PublishesDamageWhenConfidenceClearsThreshold()
    {
        Claim("a", "b");
        model.WithVerdict("a", damaged: false, confidence: 0.8).WithVerdict("b", damaged: true, confidence: 0.95);
        SetupDownloads("a", "b");

        var published = CaptureEvent();
        await CreateProcessor(threshold: 0.7).ProcessNextAsync(CancellationToken.None);

        Assert.NotNull(published.Value);
        Assert.True(published.Value.DamageDetected);
        Assert.Equal(0.95, published.Value.Confidence);
        Assert.Equal(ShipmentId, published.Value.ShipmentId);
        Assert.Equal("ai-inspection-service.package-integrity-assessed", published.RoutingKey);
    }

    [Fact]
    public async Task ProcessNextAsync_IgnoresDamageBelowThreshold()
    {
        // «Повреждено» с уверенностью 0.65 — догадка модели; на складе от такой тревоги вреда
        // больше, чем пользы, поэтому событие сообщает «цело».
        Claim("a", "b");
        model.WithVerdict("a", damaged: false, confidence: 0.9).WithVerdict("b", damaged: true, confidence: 0.65);
        SetupDownloads("a", "b");

        var published = CaptureEvent();
        await CreateProcessor(threshold: 0.7).ProcessNextAsync(CancellationToken.None);

        Assert.NotNull(published.Value);
        Assert.False(published.Value.DamageDetected);

        // Уверенность «цело» — наименьшая оценка целостности по снимкам: у слабо повреждённого
        // снимка это 1 - 0.65.
        Assert.Equal(0.35, published.Value.Confidence, 6);
    }

    [Fact]
    public async Task ProcessNextAsync_ReportsWeakestIntactPhotoWhenNothingIsDamaged()
    {
        Claim("a", "b");
        model.WithVerdict("a", damaged: false, confidence: 0.92).WithVerdict("b", damaged: false, confidence: 0.71);
        SetupDownloads("a", "b");

        var published = CaptureEvent();
        await CreateProcessor().ProcessNextAsync(CancellationToken.None);

        Assert.NotNull(published.Value);
        Assert.False(published.Value.DamageDetected);
        Assert.Equal(0.71, published.Value.Confidence, 6);
    }

    [Fact]
    public async Task ProcessNextAsync_FailsJobWhenPhotoIsMissing()
    {
        var job = Claim("a", "b");
        SetupDownloads("a");
        storage.Setup(client => client.DownloadAsync(job.PhotoFileIds[1], It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        string? reason = null;
        jobs.Setup(repository => repository.FailAsync(job.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, CancellationToken>((_, value, _) => reason = value)
            .Returns(Task.CompletedTask);

        var processed = await CreateProcessor().ProcessNextAsync(CancellationToken.None);

        Assert.True(processed);
        Assert.Contains(job.PhotoFileIds[1].ToString(), reason);

        // Вердикта нет — событие не публикуется: подписчик записал бы оценку, ни на чём не
        // основанную. Незакрытых результатов тоже нет.
        outbox.Verify(
            writer => writer.Enqueue(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()),
            Times.Never);
        jobs.Verify(
            repository => repository.CompleteAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<InspectionResult>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessNextAsync_FailsJobWhenStorageThrows()
    {
        var job = Claim("a");
        storage.Setup(client => client.DownloadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("storage is down"));

        string? reason = null;
        jobs.Setup(repository => repository.FailAsync(job.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, CancellationToken>((_, value, _) => reason = value)
            .Returns(Task.CompletedTask);

        // Сбой закрывает задание, а не роняет рабочий цикл: очередь должна разбираться дальше.
        var processed = await CreateProcessor().ProcessNextAsync(CancellationToken.None);

        Assert.True(processed);
        Assert.Equal("storage is down", reason);
    }

    private InspectionProcessor CreateProcessor(double threshold = 0.7) =>
        new(jobs.Object,
            storage.Object,
            model,
            outbox.Object,
            new InspectionOptions { DamageConfidenceThreshold = threshold },
            NullLogger<InspectionProcessor>.Instance);

    private InspectionJob Claim(params string[] photoNames)
    {
        var job = new InspectionJob
        {
            Id = Guid.NewGuid(),
            ShipmentId = ShipmentId,
            PhotoFileIds = photoNames.Select(_ => Guid.NewGuid()).ToList(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        jobs.Setup(repository => repository.ClaimNextQueuedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(job)
            .Callback(() => jobs
                .Setup(repository => repository.ClaimNextQueuedAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((InspectionJob?)null));

        photoNames.Zip(job.PhotoFileIds).ToList().ForEach(pair => names[pair.Second] = pair.First);

        return job;
    }

    private readonly Dictionary<Guid, string> names = [];

    private void SetupDownloads(params string[] photoNames)
    {
        foreach (var (fileId, name) in names.Where(entry => photoNames.Contains(entry.Value)))
        {
            storage.Setup(client => client.DownloadAsync(fileId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(FakeInspectionModel.Image(name));
        }
    }

    private List<InspectionResult> CaptureResults()
    {
        var written = new List<InspectionResult>();
        jobs.Setup(repository => repository.CompleteAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyList<InspectionResult>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyList<InspectionResult>, CancellationToken>((_, results, _) => written.AddRange(results))
            .Returns(Task.CompletedTask);

        return written;
    }

    private PublishedEvent CaptureEvent()
    {
        var published = new PublishedEvent();
        outbox.Setup(writer => writer.Enqueue(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Callback<Guid, string, string, DateTimeOffset>((_, routingKey, payload, _) =>
            {
                published.RoutingKey = routingKey;
                published.Value = JsonSerializer.Deserialize<PackageIntegrityAssessed>(payload);
            });

        return published;
    }

    private sealed class PublishedEvent
    {
        public string? RoutingKey { get; set; }
        public PackageIntegrityAssessed? Value { get; set; }
    }
}
