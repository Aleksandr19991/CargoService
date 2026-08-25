namespace CargoService.Application.Models;

/// <summary>Вердикт ai-inspection-service по фото упаковки (событие PackageIntegrityAssessed).</summary>
public sealed record PackageIntegrityAssessment
{
    public required Guid InspectionJobId { get; init; }

    /// <summary>Модель считает, что упаковка повреждена.</summary>
    public required bool DamageDetected { get; init; }

    /// <summary>Уверенность модели, 0..1.</summary>
    public required double Confidence { get; init; }
}
