namespace AiInspectionService.Application.Interfaces;

public interface IFileStorageClient
{
    /// <summary>
    /// Скачивает содержимое файла. <c>null</c> — файла в хранилище нет: снимок мог быть удалён
    /// между постановкой задания и инференсом, и это не сбой связи, а другое положение дел,
    /// которое конвейер обрабатывает иначе.
    /// </summary>
    Task<byte[]?> DownloadAsync(Guid fileId, CancellationToken cancellationToken);
}
