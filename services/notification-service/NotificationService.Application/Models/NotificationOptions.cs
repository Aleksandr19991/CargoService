namespace NotificationService.Application.Models;

/// <summary>
/// Настройки самих уведомлений (в отличие от настроек каналов, которые живут у отправителей).
/// Заполняется из конфигурации в Infrastructure — Application про <c>IConfiguration</c> не знает.
/// </summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>
    /// Общий адрес сотрудников для служебных уведомлений (сегодня — расхождение оценки упаковки
    /// с вердиктом ИИ). Пустая строка означает, что служебные уведомления никуда не уходят:
    /// адресата у них нет, и это осознанная настройка стенда, а не сбой.
    /// </summary>
    public string StaffEmail { get; init; } = string.Empty;
}
