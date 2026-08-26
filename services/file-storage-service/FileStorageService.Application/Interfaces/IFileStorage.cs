using FileStorageService.Application.Models;

namespace FileStorageService.Application.Interfaces;

/// <summary>
/// Обёртка над S3-совместимым хранилищем. Сервис не проксирует содержимое файлов — он выдаёт
/// presigned-ссылки, а байты клиент льёт и забирает напрямую из хранилища.
/// </summary>
public interface IFileStorage
{
    /// <summary>Выдаёт идентификатор нового файла и presigned PUT-ссылку для его загрузки.</summary>
    Task<FileUploadTicket> CreateUploadTicketAsync(string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Presigned GET-ссылка на существующий файл. <c>null</c>, если файла с таким id в хранилище нет —
    /// иначе клиент получил бы рабочую на вид ссылку, отдающую 404 от самого хранилища.
    /// </summary>
    Task<FileDownloadTicket?> CreateDownloadTicketAsync(Guid fileId, CancellationToken cancellationToken = default);
}
