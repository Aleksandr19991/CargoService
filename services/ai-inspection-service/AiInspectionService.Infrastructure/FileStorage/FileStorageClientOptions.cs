namespace AiInspectionService.Infrastructure.FileStorage;

public class FileStorageClientOptions
{
    public const string SectionName = "FileStorageService";

    public required string BaseUrl { get; init; }

    /// <summary>
    /// Просить ссылки, подписанные на внутренний адрес хранилища (`minio:9000`). По умолчанию
    /// да — сервис работает внутри docker-сети. Выключается для запуска на хосте: там
    /// внутреннее имя не резолвится, а переписать хост в готовой ссылке нельзя, он входит в
    /// подпись SigV4. Та же природа, что у разделения `Keycloak:BaseUrl` и `ValidIssuer`
    /// (см. CLAUDE.md): адрес зависит от того, откуда смотрят.
    /// </summary>
    public bool UseInternalUrls { get; init; } = true;
}
