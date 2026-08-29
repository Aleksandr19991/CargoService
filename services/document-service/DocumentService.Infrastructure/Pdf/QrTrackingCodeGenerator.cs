using DocumentService.Application.Interfaces;
using QRCoder;

namespace DocumentService.Infrastructure.Pdf;

/// <summary>
/// QR-код трек-номера в PNG (QRCoder, MIT).
/// <para>
/// QR, а не линейный штрихкод: он читается телефоном получателя без специального сканера и
/// вмещает ссылку на публичный трекинг целиком, тогда как Code128 хранил бы только номер и
/// требовал бы ручного ввода адреса. Линейный код добавляется отдельной реализацией
/// <see cref="ITrackingCodeGenerator"/>, если этого потребует чужой бланк.
/// </para>
/// <para>
/// Используется <c>PngByteQRCode</c>, который рисует PNG сам, без System.Drawing и SkiaSharp:
/// генерация не зависит от графических библиотек в образе — та же забота, что с шрифтами
/// (см. <see cref="DocumentFonts"/>).
/// </para>
/// </summary>
public class QrTrackingCodeGenerator(TrackingCodeOptions options) : ITrackingCodeGenerator
{
    // Уровень коррекции M (~15%): документ печатают и он мнётся в дороге, а запас выше
    // заметно уплотняет рисунок, из-за чего мелкий код на бланке начинает хуже читаться.
    private const QRCodeGenerator.ECCLevel ErrorCorrection = QRCodeGenerator.ECCLevel.M;

    // Размер модуля в пикселях: 8 даёт ~250 px на короткую ссылку — достаточно, чтобы код
    // остался чётким при печати блока 2×2 см, и при этом не раздувает PDF.
    private const int PixelsPerModule = 8;

    public byte[] CreateTrackingCode(string trackingNumber)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
            throw new ArgumentException("Tracking number is required to build a tracking code.", nameof(trackingNumber));

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(BuildPayload(trackingNumber), ErrorCorrection);

        return new PngByteQRCode(data).GetGraphic(PixelsPerModule);
    }

    private string BuildPayload(string trackingNumber) =>
        string.IsNullOrWhiteSpace(options.PublicTrackingBaseUrl)
            ? trackingNumber
            : $"{options.PublicTrackingBaseUrl.TrimEnd('/')}/api/track/{trackingNumber}";
}
