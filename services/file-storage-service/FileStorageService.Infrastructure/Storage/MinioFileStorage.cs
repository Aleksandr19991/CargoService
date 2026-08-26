using FileStorageService.Application.Interfaces;
using FileStorageService.Application.Models;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace FileStorageService.Infrastructure.Storage;

public class MinioFileStorage(MinioClients clients, MinioOptions options, FileUploadPolicy uploadPolicy) : IFileStorage
{
    public async Task<FileUploadTicket> CreateUploadTicketAsync(string contentType, CancellationToken cancellationToken = default)
    {
        var fileId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.Add(options.UrlLifetime);

        // Ключ объекта выводится из идентификатора, поэтому отдельная таблица соответствий
        // «id ↔ объект» не нужна — у сервиса вообще нет своей БД.
        var policy = new PostPolicy();
        policy.SetBucket(options.Bucket);
        policy.SetKey(ObjectKey(fileId));
        policy.SetExpires(expiresAt.UtcDateTime);

        // Оба условия входят в подпись, поэтому проверяет их хранилище, а не мы: подменить их на
        // клиенте нельзя, не сломав подпись. Именно поэтому здесь POST, а не PUT — у подписанного
        // PUT ограничить размер нечем.
        policy.SetContentType(contentType);
        policy.SetContentRange(1, uploadPolicy.MaxFileSizeBytes);

        var (uri, formFields) = await clients.Presigning.PresignedPostPolicyAsync(policy);

        // SDK возвращает подпись и служебные поля, но само поле Content-Type в форму не кладёт,
        // хотя условие `eq $Content-Type` в политику записывает. Клиент, отправивший ровно то,
        // что мы выдали, получал бы 403 «Policy Condition failed» (проверено живым прогоном) —
        // поэтому дописываем поле сами, чтобы тикет был самодостаточным.
        var fields = new Dictionary<string, string>(formFields)
        {
            ["Content-Type"] = contentType,
        };

        return new FileUploadTicket
        {
            FileId = fileId,
            UploadUrl = uri.ToString(),
            FormFields = fields,
            MaxFileSizeBytes = uploadPolicy.MaxFileSizeBytes,
            ExpiresAt = expiresAt,
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

    public async Task<bool> ExistsAsync(Guid fileId, CancellationToken cancellationToken = default)
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
