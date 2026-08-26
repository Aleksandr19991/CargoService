namespace FileStorageService.Application.Models;

/// <summary>Разрешение на скачивание файла: presigned GET-ссылка с ограниченным сроком жизни.</summary>
public sealed record FileDownloadTicket
{
    public required Guid FileId { get; init; }
    public required string DownloadUrl { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}
