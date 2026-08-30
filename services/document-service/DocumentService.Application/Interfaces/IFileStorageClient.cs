namespace DocumentService.Application.Interfaces;

public interface IFileStorageClient
{
    /// <summary>
    /// Кладёт готовый файл в хранилище и возвращает его идентификатор. Содержимое передаётся
    /// байтами: документ и так собран в памяти, а поток здесь ничего не сэкономил бы — PDF
    /// бланка это десятки килобайт.
    /// </summary>
    Task<Guid> UploadAsync(byte[] content, string contentType, CancellationToken cancellationToken);

    /// <summary>
    /// Подписанная ссылка на скачивание — та, что отдаётся клиенту в браузер. <c>null</c>, если
    /// файла в хранилище нет: документ помечен готовым, а файла нет — это рассогласование, и
    /// молча выдавать ссылку, которая ответит 404, хуже, чем сказать об этом.
    /// </summary>
    Task<DocumentDownloadLink?> CreateDownloadLinkAsync(Guid fileId, CancellationToken cancellationToken);
}

/// <summary>Ссылка на скачивание документа и момент, когда она перестанет работать.</summary>
public sealed record DocumentDownloadLink(string Url, DateTimeOffset ExpiresAt);
