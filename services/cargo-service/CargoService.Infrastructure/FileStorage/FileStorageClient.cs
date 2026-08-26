using System.Net;
using System.Net.Http.Headers;
using CargoService.Application.Interfaces;
using CargoService.Infrastructure.Keycloak;

namespace CargoService.Infrastructure.FileStorage;

public class FileStorageClient(HttpClient httpClient, ServiceTokenProvider tokenProvider) : IFileStorageClient
{
    public async Task<IReadOnlyCollection<Guid>> FindMissingAsync(
        IReadOnlyCollection<Guid> fileIds,
        CancellationToken cancellationToken = default)
    {
        if (fileIds.Count == 0)
            return [];

        var token = await tokenProvider.GetAccessTokenAsync(cancellationToken);
        var missing = new List<Guid>();

        // Проверяем по одному: у file-storage-service нет пакетного эндпоинта, а список фото в
        // одном акте приёмки — единицы файлов. Появится пакетная проверка — заменить здесь.
        foreach (var fileId in fileIds.Distinct())
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, $"api/files/{fileId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                missing.Add(fileId);
                continue;
            }

            // Всё, кроме 404, что не 2xx — это сбой связи с хранилищем, а не отсутствие файла.
            // Молча счесть такой файл существующим значило бы записать в акт непроверенную
            // ссылку, поэтому пробрасываем исключение: пусть запрос упадёт явно.
            response.EnsureSuccessStatusCode();
        }

        return missing;
    }
}
