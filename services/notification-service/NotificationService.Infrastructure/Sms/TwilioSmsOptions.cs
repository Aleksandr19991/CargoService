namespace NotificationService.Infrastructure.Sms;

/// <summary>
/// Настройки SMS-провайдера. Реализация написана под контракт Twilio (он назван в ТЗ, §2.6),
/// но <see cref="BaseUrl"/> вынесен в конфигурацию: тот же код проверяется против локального
/// стенда, а при переезде на другого провайдера меняется одна реализация отправителя, а не
/// всё, что уведомления отправляет.
/// </summary>
public sealed class TwilioSmsOptions
{
    public const string SectionName = "Sms";

    public required string BaseUrl { get; init; }

    /// <summary>
    /// Пустые AccountSid/AuthToken означают «провайдер не подключён» — тогда вместо этого
    /// отправителя регистрируется <see cref="LoggingSmsSender"/>.
    /// </summary>
    public string? AccountSid { get; init; }
    public string? AuthToken { get; init; }

    /// <summary>Номер отправителя в формате E.164 (арендуется у провайдера).</summary>
    public string? FromNumber { get; init; }
}
