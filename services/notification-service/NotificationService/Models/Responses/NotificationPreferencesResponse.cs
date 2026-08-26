namespace NotificationService.API.Models.Responses;

public sealed record NotificationPreferencesResponse
{
    public required bool EmailEnabled { get; init; }
    public required bool SmsEnabled { get; init; }
}
