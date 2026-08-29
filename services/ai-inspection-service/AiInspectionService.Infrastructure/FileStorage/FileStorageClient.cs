using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AiInspectionService.Application.Interfaces;
using AiInspectionService.Infrastructure.Keycloak;

namespace AiInspectionService.Infrastructure.FileStorage;

/// <summary>
/// Скачивает снимки из file-storage-service.
/// <para>
/// Две ступени: у сервиса запрашивается presigned-ссылка (с токеном сервисной учётной записи),
/// затем байты берутся прямо из хранилища. Сам file-storage-service содержимое не проксирует —
/// это его сознательное устройство (см. §3.4), и потребителю остаётся тот же путь, которым
/// ходит браузер.
/// </para>
/// <para>
/// Ссылка запрашивается с <c>?internal=true</c>: подпись SigV4 включает хост, поэтому ссылка,
/// выданная для браузера (<c>localhost:9000</c>), из контейнера ведёт в сам контейнер, а
/// переписать в ней хост нельзя — хранилище ответит 403 (эта ошибка уже ловилась в Фазе 11).
/// </para>
/// </summary>
public class FileStorageClient(
    HttpClient httpClient,
    FileStorageClientOptions options,
    ServiceTokenProvider tokenProvider) : IFileStorageClient
{
    public async Task<byte[]?> DownloadAsync(Guid fileId, CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetAccessTokenAsync(cancellationToken);

        var internalUrls = options.UseInternalUrls.ToString().ToLowerInvariant();
        using var ticketRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/files/{fileId}/download-url?internal={internalUrls}");
        ticketRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var ticketResponse = await httpClient.SendAsync(ticketRequest, cancellationToken);

        if (ticketResponse.StatusCode == HttpStatusCode.NotFound)
            return null;

        ticketResponse.EnsureSuccessStatusCode();

        var ticket = await ticketResponse.Content.ReadFromJsonAsync<DownloadUrlResponse>(cancellationToken)
            ?? throw new InvalidOperationException($"Empty download ticket for file {fileId}.");

        // Токен сервиса сюда не идёт: ссылка уже подписана, а лишний заголовок Authorization
        // хранилище разбирает как свою схему аутентификации и отвергает запрос.
        using var fileResponse = await httpClient.GetAsync(ticket.DownloadUrl, cancellationToken);
        fileResponse.EnsureSuccessStatusCode();

        return await fileResponse.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private sealed record DownloadUrlResponse
    {
        public required Guid FileId { get; init; }
        public required string DownloadUrl { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
    }
}
