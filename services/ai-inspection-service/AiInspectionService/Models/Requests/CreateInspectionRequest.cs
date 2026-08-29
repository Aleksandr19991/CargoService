namespace AiInspectionService.API.Models.Requests;

/// <summary>
/// Ручной запуск проверки: обычно повторный, когда предыдущая упала или когда сотрудник
/// сомневается в вердикте. Снимки передаются явно — тот же список, что в акте приёмки
/// cargo-service; сервис их не подбирает сам, потому что своей копии акта у него нет.
/// </summary>
public sealed record CreateInspectionRequest
{
    public required Guid ShipmentId { get; init; }
    public required List<Guid> PhotoFileIds { get; init; }
}
