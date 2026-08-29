namespace DocumentService.Application.Interfaces;

/// <summary>
/// Машиночитаемый код трек-номера для печати в документах.
/// <para>
/// Интерфейс отдаёт PNG, а не «QR» или «штрихкод»: документу всё равно, какой символикой
/// закодирован номер, а сменить её (например, на линейный Code128, если этого потребует бланк
/// перевозчика) — дело одной реализации в Infrastructure.
/// </para>
/// </summary>
public interface ITrackingCodeGenerator
{
    byte[] CreateTrackingCode(string trackingNumber);
}
