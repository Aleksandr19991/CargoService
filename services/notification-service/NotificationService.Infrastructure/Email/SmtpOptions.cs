namespace NotificationService.Infrastructure.Email;

/// <summary>
/// Настройки SMTP. Через них подключается и локальный dev-сборщик почты (Mailpit в
/// docker-compose), и настоящий провайдер: SendGrid, Mailgun и прочие принимают почту по тому
/// же SMTP, поэтому отдельной реализации под их HTTP API не требуется — меняются только
/// хост, порт и учётные данные.
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public required string Host { get; init; }
    public required int Port { get; init; }

    /// <summary>
    /// Обязателен ли STARTTLS. У провайдеров — да; у локального Mailpit шифрования нет вовсе,
    /// поэтому значение вынесено в конфигурацию, а не захардкожено.
    /// </summary>
    public required bool UseStartTls { get; init; }

    /// <summary>Пустые UserName/Password означают отправку без аутентификации (локальный сборщик почты).</summary>
    public string? UserName { get; init; }
    public string? Password { get; init; }

    public required string FromAddress { get; init; }
    public required string FromName { get; init; }
}
