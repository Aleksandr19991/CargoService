namespace FileStorageService.API.Models.Requests;

public sealed record CreateUploadUrlRequest
{
    /// <summary>MIME-тип загружаемого файла. Ограничение набора типов — задача 2 Фазы 11.</summary>
    public required string ContentType { get; init; }
}
