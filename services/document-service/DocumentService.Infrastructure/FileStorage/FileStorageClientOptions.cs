namespace DocumentService.Infrastructure.FileStorage;

public class FileStorageClientOptions
{
    public const string SectionName = "FileStorageService";

    public required string BaseUrl { get; init; }

    /// <summary>
    /// Просить разрешения на загрузку, подписанные на внутренний адрес хранилища
    /// (<c>minio:9000</c>). По умолчанию да — сервис работает внутри docker-сети. Выключается
    /// для запуска на хосте: там внутреннее имя не резолвится, а переписать хост в подписанном
    /// запросе нельзя, он входит в подпись SigV4. Та же природа, что у разделения
    /// `Keycloak:BaseUrl` и `ValidIssuer` (см. CLAUDE.md).
    /// <para>
    /// Ссылку на скачивание сервис всегда просит публичную: её он отдаёт в браузер клиента.
    /// </para>
    /// </summary>
    public bool UseInternalUrls { get; init; } = true;
}
