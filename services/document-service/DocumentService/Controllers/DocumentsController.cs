using System.Security.Claims;
using DocumentService.API.Models.Requests;
using DocumentService.API.Models.Responses;
using DocumentService.Application.Interfaces;
using DocumentService.Domain.Enums;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentService.API.Controllers;

/// <summary>
/// Выдача документов по заявке и по грузу (spec.md §3.1).
/// <para>
/// Доступ есть и у клиента, и у сотрудников, но видят они разное: клиент — только документы по
/// своим заявкам (проверяется по владельцу заявки из read-модели), сотрудник — любые. Роли
/// перечислены явно, а не заменены на «любой аутентифицированный»: у сервисных учётных записей
/// тоже есть токены, и им в личных документах клиента делать нечего.
/// </para>
/// </summary>
[Route("api/documents")]
[ApiController]
[Authorize(Roles = "Client,WarehouseOperator,Manager,Admin")]
public class DocumentsController(
    IDocumentsQueryService documents,
    IFileStorageClient fileStorage,
    IMapper mapper) : ControllerBase
{
    private static readonly string[] StaffRoles = ["WarehouseOperator", "Manager", "Admin"];

    [HttpGet]
    public async Task<ActionResult<List<DocumentResponse>>> GetDocuments(
        [FromQuery] GetDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        var access = GetAccess();

        var found = request.OrderId is { } orderId
            ? await documents.GetByOrderAsync(orderId, access, cancellationToken)
            : await documents.GetByShipmentAsync(request.ShipmentId!.Value, access, cancellationToken);

        // Чужая заявка отдаёт пустой список, а не 403: иначе по коду ответа можно было бы
        // выяснять, существует ли заявка с таким идентификатором.
        return Ok(mapper.Map<List<DocumentResponse>>(found));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DocumentResponse>> GetDocument(Guid id, CancellationToken cancellationToken)
    {
        var document = await documents.GetByIdAsync(id, GetAccess(), cancellationToken);
        if (document is null)
            return NotFound();

        return Ok(mapper.Map<DocumentResponse>(document));
    }

    /// <summary>
    /// Ссылка на скачивание PDF. Отдельным запросом, а не полем списка: ссылка подписана и
    /// живёт минуты — в списке она успела бы протухнуть раньше, чем по ней кликнут.
    /// </summary>
    [HttpGet("{id}/download-url")]
    public async Task<ActionResult<DocumentDownloadResponse>> GetDownloadUrl(Guid id, CancellationToken cancellationToken)
    {
        var document = await documents.GetByIdAsync(id, GetAccess(), cancellationToken);
        if (document is null)
            return NotFound();

        if (document.Status != DocumentStatus.Ready || document.FileId is null)
        {
            // Документ есть, но файла ещё (или уже) нет: он в очереди на печать или упал.
            // Это не «не найдено» и не ошибка запроса, а состояние документа.
            return Conflict(new ProblemDetails
            {
                Title = "Документ ещё не сформирован",
                Detail = $"Статус документа: {document.Status}. {document.FailureReason}".TrimEnd(),
            });
        }

        var link = await fileStorage.CreateDownloadLinkAsync(document.FileId.Value, cancellationToken);
        if (link is null)
        {
            // Документ помечен готовым, а файла в хранилище нет — рассогласование, о котором
            // лучше сказать, чем выдать ссылку, отвечающую 404.
            return Conflict(new ProblemDetails
            {
                Title = "Файл документа недоступен",
                Detail = "Документ помечен сформированным, но файла нет в хранилище.",
            });
        }

        return Ok(new DocumentDownloadResponse
        {
            DocumentId = document.Id,
            DownloadUrl = link.Url,
            ExpiresAt = link.ExpiresAt,
        });
    }

    private DocumentAccess GetAccess()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Token has no 'sub' claim."));

        return StaffRoles.Any(User.IsInRole)
            ? DocumentAccess.Staff(userId)
            : DocumentAccess.Client(userId);
    }
}
