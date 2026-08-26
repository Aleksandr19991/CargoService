using FileStorageService.Application.Interfaces;
using FileStorageService.Application.Models;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace FileStorageService.Infrastructure.Storage;

public class MinioFileStorage(MinioClients clients, MinioOptions options) : IFileStorage
{
    public async Task<FileUploadTicket> CreateUploadTicketAsync(string contentType, CancellationToken cancellationToken = default)
    {
        var fileId = Guid.NewGuid();

        // Ключ объекта выводится из идентификатора, поэтому отдельная таблица соответствий
        // «id ↔ объект» не нужна — у сервиса вообще нет своей БД.
        var url = await clients.Presigning.PresignedPutObjectAsync(new PresignedPutObjectArgs()
            .WithBucket(options.Bucket)
            .WithObject(ObjectKey(fileId))
            .WithExpiry((int)options.UrlLifetime.TotalSeconds));

        return new FileUploadTicket
        {
            FileId = fileId,
            UploadUrl = url,
            ExpiresAt = DateTimeOffset.UtcNow.Add(options.UrlLifetime),
        };
    }

    public async Task<FileDownloadTicket?> CreateDownloadTicketAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        // Presigned-ссылку хранилище выдаст на любой ключ, существует объект или нет, — поэтому
        // проверяем наличие сами, иначе клиент получил бы рабочую на вид ссылку с 404 внутри.
        if (!await ExistsAsync(fileId, cancellationToken))
            return null;

        var url = await clients.Presigning.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(options.Bucket)
            .WithObject(ObjectKey(fileId))
            .WithExpiry((int)options.UrlLifetime.TotalSeconds));

        return new FileDownloadTicket
        {
            FileId = fileId,
            DownloadUrl = url,
            ExpiresAt = DateTimeOffset.UtcNow.Add(options.UrlLifetime),
        };
    }

    private async Task<bool> ExistsAsync(Guid fileId, CancellationToken cancellationToken)
    {
        try
        {
            // Настоящий сетевой вызов — идёт по внутреннему адресу.
            await clients.Operations.StatObjectAsync(new StatObjectArgs()
                .WithBucket(options.Bucket)
                .WithObject(ObjectKey(fileId)), cancellationToken);

            return true;
        }
        catch (ObjectNotFoundException)
        {
            return false;
        }
    }

    private static string ObjectKey(Guid fileId) => fileId.ToString("N");
}
