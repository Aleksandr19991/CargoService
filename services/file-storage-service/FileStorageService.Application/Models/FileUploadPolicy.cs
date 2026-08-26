namespace FileStorageService.Application.Models;

/// <summary>
/// Что вообще разрешено загружать. Живёт в Application, а не рядом с настройками хранилища:
/// это правило предметной области, и на него опирается и валидатор запроса, и сама политика,
/// которую подписывает Infrastructure.
/// </summary>
public sealed record FileUploadPolicy
{
    public required long MaxFileSizeBytes { get; init; }

    /// <summary>Белый список MIME-типов. Всё, чего в нём нет, отклоняется до обращения к хранилищу.</summary>
    public required IReadOnlyCollection<string> AllowedContentTypes { get; init; }

    public bool IsContentTypeAllowed(string contentType) =>
        AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);
}
