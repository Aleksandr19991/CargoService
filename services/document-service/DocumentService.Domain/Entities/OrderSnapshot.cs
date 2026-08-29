namespace DocumentService.Domain.Entities;

/// <summary>
/// Сведения о заявке, нужные для печати, — локальная read-модель по событиям
/// <c>OrderCreated</c> и <c>OrderConfirmed</c>.
/// <para>
/// События груза несут только <c>OrderId</c> и трек-номер, а в накладной должны стоять стороны,
/// города, характеристики груза, стоимость и срок. Спрашивать их синхронно в orders-service на
/// каждую печать значило бы поставить выпуск документа в зависимость от доступности чужого
/// сервиса — тот же выбор и по тем же причинам, что в notification-service (Фаза 6).
/// </para>
/// </summary>
public class OrderSnapshot
{
    public Guid OrderId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;
    public string OriginCity { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;

    public string SenderName { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;

    public string CargoName { get; set; } = string.Empty;
    public decimal CargoWeightKg { get; set; }
    public decimal CargoVolumeM3 { get; set; }
    public decimal DeclaredValue { get; set; }

    /// <summary>Стоимость и срок приезжают позже, событием <c>OrderConfirmed</c>.</summary>
    public decimal? CalculatedPrice { get; set; }
    public DateTimeOffset? DeliveryDeadline { get; set; }
}
