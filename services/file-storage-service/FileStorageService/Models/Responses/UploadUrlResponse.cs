namespace FileStorageService.API.Models.Responses;

/// <summary>
/// Разрешение на загрузку. Клиент отправляет на <see cref="UploadUrl"/> запрос
/// <c>multipart/form-data</c>, где сначала идут все пары из <see cref="FormFields"/>, а последним —
/// поле <c>file</c> с содержимым. Ограничения по типу и размеру входят в подпись, поэтому их
/// проверяет само хранилище.
/// </summary>
public sealed record UploadUrlResponse
{
    /// <summary>Идентификатор файла — его сервисы-потребители кладут к себе (например, в PhotoFileIds).</summary>
    public required Guid FileId { get; init; }

    public required string UploadUrl { get; init; }
    public required IReadOnlyDictionary<string, string> FormFields { get; init; }

    /// <summary>Предел из подписанной политики — чтобы клиент мог отсеять файл, не начиная загрузку.</summary>
    public required long MaxFileSizeBytes { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}
