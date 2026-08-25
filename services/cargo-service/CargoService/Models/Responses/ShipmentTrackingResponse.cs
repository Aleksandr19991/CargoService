using CargoService.Domain.Enums;

namespace CargoService.API.Models.Responses;

/// <summary>
/// Ответ публичного трекинга. Эндпоинт анонимный, а трек-номер знают и отправитель, и
/// получатель, поэтому набор полей минимальный. Наружу сознательно НЕ отдаются:
/// <list type="bullet">
/// <item><c>Id</c> и <c>OrderId</c> — внутренние идентификаторы, второй ещё и ведёт в чужой сервис;</item>
/// <item>акты приёмки — там <c>InspectedByUserId</c> (личность сотрудника), состояние груза и файлы фото;</item>
/// <item>услуги упаковки — там <c>PerformedByUserId</c>;</item>
/// <item>комментарии в истории — внутренние пометки склада.</item>
/// </list>
/// Полную карточку сотрудники смотрят через <c>GET /api/shipments/{id}</c>, клиент — в личном
/// кабинете orders-service.
/// </summary>
public sealed record ShipmentTrackingResponse
{
    public required string TrackingNumber { get; init; }
    public required ShipmentStatus CurrentStatus { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    public required List<ShipmentTrackingHistoryResponse> History { get; init; }
}
