using Microsoft.Extensions.Configuration;

namespace FileStorageService.Infrastructure.Storage;

public class MinioOptions
{
    public const string SectionName = "Minio";

    /// <summary>Адрес S3 API без схемы (`host:port`) — этого формата ждёт SDK MinIO.</summary>
    public required string Endpoint { get; init; }

    public required string AccessKey { get; init; }
    public required string SecretKey { get; init; }

    /// <summary>TLS до хранилища. В локальном docker-compose MinIO поднят по http, поэтому false.</summary>
    public required bool UseSsl { get; init; }

    /// <summary>Бакет, в котором лежат все файлы платформы; создаётся при старте, если его нет.</summary>
    public required string Bucket { get; init; }

    /// <summary>
    /// Сколько живут presigned-ссылки. Короткий срок ограничивает окно, в течение которого утёкшая
    /// ссылка даёт доступ к файлу, но должен оставлять время на саму передачу.
    /// </summary>
    public required TimeSpan UrlLifetime { get; init; }

    /// <summary>
    /// Адрес, по которому хранилище видно снаружи, если он отличается от <see cref="Endpoint"/>.
    /// Нужен по той же причине, что и Keycloak:BaseUrl vs ValidIssuer (см. CLAUDE.md): внутри
    /// docker-сети сервис ходит в MinIO по `minio:9000`, а браузер клиента, которому достаётся
    /// presigned-ссылка, знает хранилище только как `localhost:9000`. На этом адресе строится
    /// отдельный клиент для расчёта подписи (см. <see cref="MinioClients"/>) — переписать хост в
    /// уже готовой ссылке нельзя, он входит в подпись. Null — снаружи и изнутри адрес одинаков.
    /// </summary>
    public string? PublicEndpoint { get; init; }

    /// <summary>Предельный размер загружаемого файла; попадает в подписанную policy.</summary>
    public required long MaxFileSizeBytes { get; init; }

    /// <summary>Белый список MIME-типов, разрешённых к загрузке.</summary>
    public required IReadOnlyCollection<string> AllowedContentTypes { get; init; }

    public static MinioOptions Bind(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);

        return new MinioOptions
        {
            Endpoint = section["Endpoint"] ?? throw new InvalidOperationException("Minio:Endpoint is not configured."),
            AccessKey = section["AccessKey"] ?? throw new InvalidOperationException("Minio:AccessKey is not configured."),
            SecretKey = section["SecretKey"] ?? throw new InvalidOperationException("Minio:SecretKey is not configured."),
            UseSsl = bool.Parse(section["UseSsl"] ?? "false"),
            Bucket = section["Bucket"] ?? throw new InvalidOperationException("Minio:Bucket is not configured."),
            UrlLifetime = TimeSpan.FromSeconds(int.Parse(section["UrlLifetimeSeconds"] ?? "900")),
            PublicEndpoint = section["PublicEndpoint"],
            MaxFileSizeBytes = long.Parse(section["MaxFileSizeBytes"] ?? throw new InvalidOperationException("Minio:MaxFileSizeBytes is not configured.")),
            AllowedContentTypes = [.. section.GetSection("AllowedContentTypes").GetChildren().Select(child => child.Value!)],
        };
    }
}
