using NotificationService.Domain.Enums;

namespace NotificationService.API.Models.Requests;

public sealed record GetNotificationsRequest
{
    /// <summary>Фильтр по каналу; не задан — вся история клиента.</summary>
    public NotificationChannel? Channel { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
