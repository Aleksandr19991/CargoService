using FileStorageService.API.Models.Requests;
using FileStorageService.API.Models.Responses;
using FileStorageService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FileStorageService.API.Controllers;

/// <summary>
/// Единая точка хранения бинарных файлов платформы (spec.md §3.4). Сервис не проксирует содержимое:
/// он выдаёт presigned-ссылки, а клиент льёт и забирает байты напрямую из объектного хранилища —
/// иначе загрузка каждого скана занимала бы поток и память этого сервиса.
/// </summary>
[Route("api/files")]
[ApiController]
[Authorize]
public class FilesController(IFileStorage fileStorage) : ControllerBase
{
    // Файлы в системе заводят сотрудники: фото приёмки делает склад, сканы документов — бэк-офис.
    private const string UploadRoles = "WarehouseOperator,Manager,Admin";

    [HttpPost("upload-url")]
    [Authorize(Roles = UploadRoles)]
    public async Task<ActionResult<UploadUrlResponse>> CreateUploadUrl(
        [FromBody] CreateUploadUrlRequest request,
        CancellationToken cancellationToken)
    {
        var ticket = await fileStorage.CreateUploadTicketAsync(request.ContentType, cancellationToken);

        return Ok(new UploadUrlResponse
        {
            FileId = ticket.FileId,
            UploadUrl = ticket.UploadUrl,
            FormFields = ticket.FormFields,
            MaxFileSizeBytes = ticket.MaxFileSizeBytes,
            ExpiresAt = ticket.ExpiresAt,
        });
    }

    /// <summary>
    /// Существует ли файл. Отдельно от выдачи ссылки, потому что вызывают это по другому поводу:
    /// сервисы, хранящие у себя ссылки на файлы (PhotoFileIds в акте приёмки у cargo-service),
    /// проверяют, что за идентификатором действительно что-то стоит. Просить ради этого ссылку на
    /// скачивание было бы враньём о намерении и лишней работой по подписи.
    /// </summary>
    [HttpHead("{fileId}")]
    public async Task<IActionResult> FileExists(Guid fileId, CancellationToken cancellationToken)
    {
        var exists = await fileStorage.ExistsAsync(fileId, cancellationToken);
        return exists ? Ok() : NotFound();
    }

    /// <summary>
    /// Ссылка на скачивание. Доступна любому аутентифицированному пользователю: файл сам по себе
    /// ничего не выдаёт о том, к какой заявке он относится, а знать его id можно, только получив
    /// его из сервиса, который уже проверил права (карточка груза, документы по заявке).
    /// </summary>
    [HttpGet("{fileId}/download-url")]
    public async Task<ActionResult<DownloadUrlResponse>> CreateDownloadUrl(Guid fileId, CancellationToken cancellationToken)
    {
        var ticket = await fileStorage.CreateDownloadTicketAsync(fileId, cancellationToken);
        if (ticket is null)
            return NotFound();

        return Ok(new DownloadUrlResponse
        {
            FileId = ticket.FileId,
            DownloadUrl = ticket.DownloadUrl,
            ExpiresAt = ticket.ExpiresAt,
        });
    }
}
