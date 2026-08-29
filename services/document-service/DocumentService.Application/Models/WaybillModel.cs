namespace DocumentService.Application.Models;

/// <summary>
/// Данные транспортной накладной.
/// <para>
/// Часть полей необязательна не потому, что документ их не требует, а потому, что сервис
/// получает сведения из событий, и не все они там есть: номер заявки и города приезжают в
/// <c>OrderCreated</c>, стоимость и срок — в <c>OrderConfirmed</c>, трек-номер — из событий
/// груза. Отправитель и получатель в контрактах не передаются вовсе — откуда их брать, решает
/// задача 4 Фазы 8 (read-модель по событиям заявки либо расширение контракта). Незаполненное
/// поле печатается прочерком, а не пропадает: пустая строка в бланке видна, отсутствующая — нет.
/// </para>
/// </summary>
public sealed record WaybillModel
{
    public required string TrackingNumber { get; init; }
    public required DateTimeOffset IssuedAt { get; init; }

    public string? OrderNumber { get; init; }
    public string? OriginCity { get; init; }
    public string? DestinationCity { get; init; }
    public string? SenderName { get; init; }
    public string? RecipientName { get; init; }
    public decimal? WeightKg { get; init; }
    public decimal? VolumeM3 { get; init; }
    public decimal? DeclaredValue { get; init; }
    public decimal? Price { get; init; }
    public DateTimeOffset? DeliveryDeadline { get; init; }
}
