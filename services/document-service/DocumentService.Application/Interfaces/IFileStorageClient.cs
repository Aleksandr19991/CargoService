namespace DocumentService.Application.Interfaces;

public interface IFileStorageClient
{
    /// <summary>
    /// Кладёт готовый файл в хранилище и возвращает его идентификатор. Содержимое передаётся
    /// байтами: документ и так собран в памяти, а поток здесь ничего не сэкономил бы — PDF
    /// бланка это десятки килобайт.
    /// </summary>
    Task<Guid> UploadAsync(byte[] content, string contentType, CancellationToken cancellationToken);
}
