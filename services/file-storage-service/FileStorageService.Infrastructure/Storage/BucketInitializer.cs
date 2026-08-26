using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Minio.DataModel.Args;

namespace FileStorageService.Infrastructure.Storage;

/// <summary>
/// Создаёт бакет при старте, если его ещё нет — тот же принцип, что и с EF-миграциями в остальных
/// сервисах: поднятый на чистом окружении сервис должен быть сразу работоспособен, без ручных
/// шагов в консоли MinIO. Идемпотентно.
/// </summary>
public class BucketInitializer(
    MinioClients clients,
    MinioOptions options,
    ILogger<BucketInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var exists = await clients.Operations.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(options.Bucket), cancellationToken);

        if (exists)
            return;

        await clients.Operations.MakeBucketAsync(new MakeBucketArgs().WithBucket(options.Bucket), cancellationToken);
        logger.LogInformation("Created missing object storage bucket {Bucket}", options.Bucket);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
