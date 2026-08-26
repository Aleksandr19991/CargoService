using FileStorageService.Application.Models;

namespace FileStorageService.Application.Interfaces;

/// <summary>
/// Обёртка над S3-совместимым хранилищем. Сервис не проксирует содержимое файлов — он выдаёт
/// подписанные разрешения, а байты клиент льёт и забирает напрямую из хранилища.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Выдаёт идентификатор нового файла и подписанную политику загрузки. Ограничения по типу и
    /// размеру входят в подпись, поэтому их проверяет само хранилище — сервис не может быть
    /// обойдён клиентом, который решит отправить файл побольше.
    /// </summary>
    Task<FileUploadTicket> CreateUploadTicketAsync(string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Presigned GET-ссылка на существующий файл. <c>null</c>, если файла с таким id в хранилище нет —
    /// иначе клиент получил бы рабочую на вид ссылку, отдающую 404 от самого хранилища.
    /// </summary>
    Task<FileDownloadTicket?> CreateDownloadTicketAsync(Guid fileId, CancellationToken cancellationToken = default);
}
