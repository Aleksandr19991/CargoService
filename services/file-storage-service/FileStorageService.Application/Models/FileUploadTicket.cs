namespace FileStorageService.Application.Models;

/// <summary>
/// Разрешение на загрузку одного файла — presigned POST. Байты идут напрямую в объектное
/// хранилище, через сам сервис они не проходят.
/// <para>
/// Почему POST, а не PUT: подписанный PUT не позволяет ограничить размер — хранилище примет
/// сколько угодно, и проверить размер можно было бы только постфактум, когда трафик уже
/// потрачен, а файл уже лежит. У POST есть policy с условиями, которые проверяет само
/// хранилище: слишком большой или не того типа файл отвергается на его стороне.
/// </para>
/// </summary>
public sealed record FileUploadTicket
{
    /// <summary>Идентификатор, под которым файл будет известен остальным сервисам (например, в PhotoFileIds у cargo-service).</summary>
    public required Guid FileId { get; init; }

    /// <summary>Адрес, на который клиент отправляет multipart/form-data.</summary>
    public required string UploadUrl { get; init; }

    /// <summary>
    /// Поля формы, которые нужно отправить вместе с файлом (подпись, политика, ключ объекта).
    /// Сам файл добавляется последним полем с именем <c>file</c>.
    /// </summary>
    public required IReadOnlyDictionary<string, string> FormFields { get; init; }

    /// <summary>Предельный размер, зашитый в подписанную политику — чтобы клиент мог отсеять файл заранее.</summary>
    public required long MaxFileSizeBytes { get; init; }

    /// <summary>Момент, после которого разрешение перестаёт действовать.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}
