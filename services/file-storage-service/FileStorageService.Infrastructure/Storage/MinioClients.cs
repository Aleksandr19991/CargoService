using Minio;

namespace FileStorageService.Infrastructure.Storage;

/// <summary>
/// Два клиента к одному хранилищу, различающиеся адресом.
/// <para>
/// <see cref="Operations"/> ходит по внутреннему адресу (`minio:9000` в docker-сети) — им делаются
/// настоящие сетевые вызовы: проверка бакета, StatObject.
/// </para>
/// <para>
/// <see cref="Presigning"/> настроен на адрес, по которому хранилище видит клиент снаружи
/// (`localhost:9000`), и используется только для расчёта presigned-ссылок. Сети он не касается:
/// подпись — чистое вычисление. Разделение нужно потому, что хост входит в подпись SigV4
/// (`X-Amz-SignedHeaders=host`), поэтому просто переписать хост в готовой ссылке нельзя —
/// хранилище ответит 403 (проверено живым прогоном).
/// </para>
/// </summary>
public sealed class MinioClients(IMinioClient operations, IMinioClient presigning)
{
    public IMinioClient Operations { get; } = operations;
    public IMinioClient Presigning { get; } = presigning;
}
