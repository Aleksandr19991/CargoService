namespace NotificationService.API.Models.Responses;

public sealed record NotificationListResponse
{
    public required List<NotificationResponse> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
}
