using CargoService.Domain.Enums;

namespace CargoService.API.Models.Responses;

public sealed record AcceptanceInspectionResponse
{
    public required Guid Id { get; init; }
    public required Guid InspectedByUserId { get; init; }
    public required PackagingCondition PackagingCondition { get; init; }
    public required CargoCondition CargoCondition { get; init; }
    public string? Comment { get; init; }
    public required List<Guid> PhotoFileIds { get; init; }
    public required DateTimeOffset InspectedAt { get; init; }

    // Вердикт ai-inspection-service. Null, пока ответа нет (или если фото не приложены).
    // Это и есть «алерт для сотрудника» из ТЗ — карточку груза он видит, а рассылку по каналам
    // делает notification-service, подписанный на то же событие.
    public bool? AiDamageDetected { get; init; }
    public double? AiConfidence { get; init; }
    public DateTimeOffset? AiAssessedAt { get; init; }
    public bool? HasAssessmentDiscrepancy { get; init; }
}
