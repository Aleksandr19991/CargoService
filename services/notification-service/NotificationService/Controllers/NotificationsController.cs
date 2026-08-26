using System.Security.Claims;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationService.API.Models.Requests;
using NotificationService.API.Models.Responses;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;

namespace NotificationService.API.Controllers;

/// <summary>
/// Личный кабинет клиента. В API §2.6 история описана как <c>GET /notifications?clientId=</c>,
/// но параметра здесь нет: получатель всегда берётся из claim <c>sub</c>, как и во всём
/// orders-service (Фаза 4) — иначе достаточно было бы подставить чужой идентификатор, чтобы
/// прочитать чужие уведомления вместе с адресом и телефоном.
/// </summary>
[Route("api/notifications")]
[ApiController]
[Authorize(Roles = "Client")]
public class NotificationsController(
    IClientNotificationsService clientNotificationsService,
    IMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NotificationListResponse>> GetNotifications(
        [FromQuery] GetNotificationsRequest request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await clientNotificationsService.GetHistoryAsync(
            GetUserId(), request.Channel, request.Page, request.PageSize, cancellationToken);

        return Ok(new NotificationListResponse
        {
            Items = mapper.Map<List<NotificationResponse>>(items),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
        });
    }

    [HttpGet("preferences")]
    public async Task<ActionResult<NotificationPreferencesResponse>> GetPreferences(CancellationToken cancellationToken)
    {
        var preferences = await clientNotificationsService.GetPreferencesAsync(GetUserId(), cancellationToken);
        return Ok(mapper.Map<NotificationPreferencesResponse>(preferences));
    }

    /// <summary>
    /// POST, а не PUT, — так эндпоинт назван в ТЗ (§2.6); по смыслу это upsert полного набора
    /// настроек, отдельного «создания» у них не бывает.
    /// </summary>
    [HttpPost("preferences")]
    public async Task<ActionResult<NotificationPreferencesResponse>> UpdatePreferences(
        [FromBody] UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        // UserId проставляется из токена, а не из тела запроса: иначе клиент менял бы чужие настройки.
        var preference = mapper.Map<NotificationPreference>(request);
        preference.UserId = GetUserId();

        var updated = await clientNotificationsService.UpdatePreferencesAsync(preference, cancellationToken);
        return Ok(mapper.Map<NotificationPreferencesResponse>(updated));
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Token has no 'sub' claim.");

        return Guid.Parse(userId);
    }
}
