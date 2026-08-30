using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DocumentService.Application.Interfaces;
using DocumentService.Infrastructure.Keycloak;

namespace DocumentService.Infrastructure.FileStorage;

/// <summary>
/// Загружает готовые PDF в file-storage-service.
/// <para>
/// Две ступени, как и у остальных потребителей хранилища: у сервиса запрашивается подписанное
/// разрешение на загрузку (токеном сервисной учётной записи), затем файл отправляется прямо в
/// хранилище multipart-формой. Сам file-storage-service содержимое не проксирует — это его
/// сознательное устройство (§3.4).
/// </para>
/// <para>
/// Поля формы отправляются ровно в том виде и порядке, в каком их выдал сервис, а <c>file</c>
/// идёт последним: политика подписи S3 проверяет поля, и файл, отправленный раньше них,
/// хранилище отвергает.
/// </para>
/// </summary>
public class FileStorageClient(HttpClient httpClient, ServiceTokenProvider tokenProvider) : IFileStorageClient
{
    public async Task<Guid> UploadAsync(byte[] content, string contentType, CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetAccessTokenAsync(cancellationToken);

        using var ticketRequest = new HttpRequestMessage(HttpMethod.Post, "api/files/upload-url")
        {
            Content = JsonContent.Create(new { contentType }),
        };
        ticketRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var ticketResponse = await httpClient.SendAsync(ticketRequest, cancellationToken);
        ticketResponse.EnsureSuccessStatusCode();

        var ticket = await ticketResponse.Content.ReadFromJsonAsync<UploadUrlResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Empty upload ticket from file-storage-service.");

        if (content.Length > ticket.MaxFileSizeBytes)
        {
            // Хранилище отвергло бы файл само (лимит входит в подпись), но узнать об этом до
            // отправки дешевле, чем после — и сообщение получится внятным.
            throw new InvalidOperationException(
                $"Документ занимает {content.Length} байт при лимите хранилища {ticket.MaxFileSizeBytes}.");
        }

        using var form = new MultipartFormDataContent();
        foreach (var (name, value) in ticket.FormFields)
            form.Add(new StringContent(value), name);

        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", "document.pdf");

        // Токен сервиса сюда не идёт: ссылка уже подписана, а лишний заголовок Authorization
        // хранилище разбирает как свою схему аутентификации и отвергает запрос.
        using var uploadResponse = await httpClient.PostAsync(ticket.UploadUrl, form, cancellationToken);
        uploadResponse.EnsureSuccessStatusCode();

        return ticket.FileId;
    }

    private sealed record UploadUrlResponse
    {
        public required Guid FileId { get; init; }
        public required string UploadUrl { get; init; }

        [JsonPropertyName("formFields")]
        public required Dictionary<string, string> FormFields { get; init; }

        public required long MaxFileSizeBytes { get; init; }
    }
}
