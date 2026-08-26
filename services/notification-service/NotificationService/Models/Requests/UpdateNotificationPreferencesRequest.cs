namespace NotificationService.API.Models.Requests;

/// <summary>
/// Полный набор настроек, а не частичная правка: каналов немного, и «прислали только Sms=false»
/// невозможно отличить от «Email не указан» без ещё одного слоя nullable-полей.
/// </summary>
public sealed record UpdateNotificationPreferencesRequest
{
    public required bool EmailEnabled { get; init; }
    public required bool SmsEnabled { get; init; }
}
