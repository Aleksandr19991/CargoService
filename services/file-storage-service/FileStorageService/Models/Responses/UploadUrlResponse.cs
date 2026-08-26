namespace FileStorageService.API.Models.Responses;

public sealed record UploadUrlResponse
{
    /// <summary>Идентификатор файла — его сервисы-потребители кладут к себе (например, в PhotoFileIds).</summary>
    public required Guid FileId { get; init; }

    /// <summary>Ссылка для прямого PUT в хранилище; через file-storage-service байты не идут.</summary>
    public required string UploadUrl { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}
