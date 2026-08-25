using CargoService.Domain.Enums;

namespace CargoService.API.Models.Responses;

/// <summary>
/// Полная карточка груза для сотрудников. Клиентская сторона видит урезанный набор полей через
/// публичный <c>GET /track/{trackingNumber}</c> (spec.md Фаза 5).
/// </summary>
public sealed record ShipmentResponse
{
    public required Guid Id { get; init; }
    public required Guid OrderId { get; init; }
    public required string TrackingNumber { get; init; }
    public required ShipmentStatus CurrentStatus { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Срок доставки из заявки; по нему джоба контроля SLA помечает груз задерживающимся.</summary>
    public DateTimeOffset? DeliveryDeadline { get; init; }

    public required List<AcceptanceInspectionResponse> Inspections { get; init; }
    public required List<PackagingServiceResponse> PackagingServices { get; init; }
    public required List<ShipmentStatusHistoryResponse> StatusHistory { get; init; }
}
