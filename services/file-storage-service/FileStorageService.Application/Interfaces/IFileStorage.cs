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
    /// <param name="forInternalNetwork">
    /// Подписать разрешение на внутренний адрес хранилища — для сервисов, которые льют файл
    /// сами изнутри docker-сети. Публичный адрес это <c>localhost</c> браузера пользователя, и
    /// из контейнера он ведёт в сам контейнер; переписать хост в готовой подписи нельзя.
    /// </param>
    Task<FileUploadTicket> CreateUploadTicketAsync(
        string contentType,
        bool forInternalNetwork = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Presigned GET-ссылка на существующий файл. <c>null</c>, если файла с таким id в хранилище нет —
    /// иначе клиент получил бы рабочую на вид ссылку, отдающую 404 от самого хранилища.
    /// <para>
    /// <paramref name="forInternalNetwork"/> подписывает ссылку на внутренний адрес хранилища
    /// вместо публичного. Нужно сервисам, которые скачивают файлы сами, изнутри docker-сети
    /// (ai-inspection-service берёт так фото на инференс): публичный адрес — это
    /// <c>localhost:9000</c> браузера пользователя, из контейнера он ведёт в сам контейнер.
    /// Переписать хост в готовой ссылке нельзя — он входит в подпись SigV4 (см. MinioClients),
    /// поэтому выбор адреса делается до подписи.
    /// </para>
    /// </summary>
    Task<FileDownloadTicket?> CreateDownloadTicketAsync(
        Guid fileId,
        bool forInternalNetwork = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Есть ли файл в хранилище. Нужен сервисам-потребителям, которые хранят у себя ссылки на
    /// файлы (например, PhotoFileIds в акте приёмки) и обязаны убедиться, что за идентификатором
    /// действительно что-то стоит.
    /// </summary>
    Task<bool> ExistsAsync(Guid fileId, CancellationToken cancellationToken = default);
}
