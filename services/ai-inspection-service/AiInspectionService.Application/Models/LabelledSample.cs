namespace AiInspectionService.Application.Models;

/// <summary>Снимок тестового набора с известным ответом: <c>IsDamaged</c> — разметка человека.</summary>
public sealed record LabelledSample
{
    public required string Name { get; init; }
    public required byte[] ImageBytes { get; init; }
    public required bool IsDamaged { get; init; }
}
