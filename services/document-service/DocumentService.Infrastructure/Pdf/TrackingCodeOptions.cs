namespace DocumentService.Infrastructure.Pdf;

public sealed class TrackingCodeOptions
{
    public const string SectionName = "Documents";

    /// <summary>
    /// Адрес публичного трекинга, из которого собирается ссылка в QR-коде. Пусто — в код
    /// попадёт голый трек-номер: сканеру склада этого достаточно, а вот получателю посылки
    /// ссылка полезнее, поэтому в развёрнутой системе адрес задают.
    /// </summary>
    public string? PublicTrackingBaseUrl { get; init; }
}
