namespace FileStorageService.Application.Models;

/// <summary>
/// Разрешение на загрузку одного файла. Байты идут напрямую в объектное хранилище по
/// <see cref="UploadUrl"/> — через сам сервис они не проходят, поэтому загрузка гигабайтного
/// скана не занимает ни его память, ни поток.
/// </summary>
public sealed record FileUploadTicket
{
    /// <summary>Идентификатор, под которым файл будет известен остальным сервисам (например, в PhotoFileIds у cargo-service).</summary>
    public required Guid FileId { get; init; }

    /// <summary>Presigned PUT-ссылка в хранилище.</summary>
    public required string UploadUrl { get; init; }

    /// <summary>Момент, после которого ссылка перестаёт работать.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}
