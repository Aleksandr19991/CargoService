using AiInspectionService.Application.Interfaces;
using AiInspectionService.Domain.Entities;
using AiInspectionService.Domain.Enums;
using Moq;

namespace AiInspectionService.Application.Tests;

public class InspectionJobsServiceTests
{
    private readonly Mock<IInspectionJobsRepository> repository = new();

    [Fact]
    public async Task EnqueueAsync_CreatesQueuedJob()
    {
        InspectionJob? created = null;
        repository
            .Setup(instance => instance.CreateAsync(It.IsAny<InspectionJob>(), It.IsAny<CancellationToken>()))
            .Callback<InspectionJob, CancellationToken>((job, _) => created = job)
            .Returns(Task.CompletedTask);

        var shipmentId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        var job = await new InspectionJobsService(repository.Object)
            .EnqueueAsync(shipmentId, [photoId], CancellationToken.None);

        Assert.NotNull(job);
        Assert.Same(created, job);
        Assert.Equal(shipmentId, job.ShipmentId);
        Assert.Equal(InspectionJobStatus.Queued, job.Status);
        Assert.Equal([photoId], job.PhotoFileIds);
        Assert.NotEqual(default, job.CreatedAt);
    }

    [Fact]
    public async Task EnqueueAsync_DropsRepeatedPhotoIds()
    {
        // Повтор внутри события иначе дошёл бы до записи вердиктов и нарушил уникальность
        // (JobId, FileId) — уже после того, как время модели на дубль потрачено.
        var photoId = Guid.NewGuid();

        var job = await new InspectionJobsService(repository.Object)
            .EnqueueAsync(Guid.NewGuid(), [photoId, photoId, photoId], CancellationToken.None);

        Assert.NotNull(job);
        Assert.Equal([photoId], job.PhotoFileIds);
    }

    [Fact]
    public async Task EnqueueAsync_ReturnsNullWhenThereAreNoPhotos()
    {
        var job = await new InspectionJobsService(repository.Object)
            .EnqueueAsync(Guid.NewGuid(), [], CancellationToken.None);

        Assert.Null(job);
        repository.Verify(
            instance => instance.CreateAsync(It.IsAny<InspectionJob>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
