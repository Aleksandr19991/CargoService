namespace AiInspectionService.API.Models.Responses;

/// <summary>
/// Вердикт по одному снимку. Сырой ответ модели (<c>RawResponse</c>) наружу не отдаётся: это
/// вектор чисел, который ничего не добавляет к оценке и уверенности рядом, а для разбора
/// спорных случаев он есть в базе.
/// </summary>
public sealed record InspectionResultResponse
{
    public required Guid FileId { get; init; }
    public required double PackagingIntegrityScore { get; init; }
    public required bool DamageDetected { get; init; }
    public required double Confidence { get; init; }
    public required List<string> DamageTypes { get; init; }
    public required string ModelVersion { get; init; }
    public required DateTimeOffset AssessedAt { get; init; }
}
