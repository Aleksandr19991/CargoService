using AiInspectionService.Domain.Enums;

namespace AiInspectionService.Domain.Entities;

/// <summary>
/// Задание на автоматическую проверку снимков одного груза: ставится событием
/// <c>CargoPhotoUploaded</c> либо вручную через <c>POST /inspections</c> (spec.md §2.7).
/// </summary>
public class InspectionJob
{
    public Guid Id { get; set; }

    // References Shipment.Id in cargo-service — no FK/navigation, «database per service»
    // кросс-сервисных связей не допускает.
    public Guid ShipmentId { get; set; }

    /// <summary>
    /// Снимки, которые нужно проверить, — идентификаторы файлов в File Storage. Как и в акте
    /// приёмки cargo-service, лежат в нативной колонке <c>uuid[]</c>: список короткий и читается
    /// всегда целиком вместе с заданием.
    /// </summary>
    public List<Guid> PhotoFileIds { get; set; } = [];

    public InspectionJobStatus Status { get; set; } = InspectionJobStatus.Queued;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Когда задание взяли в работу и когда закончили. Null, пока этого не случилось.</summary>
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Причина отказа при статусе <see cref="InspectionJobStatus.Failed"/>.</summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Вердикты по снимкам задания — по строке на снимок (см. <see cref="InspectionResult"/>).
    /// </summary>
    public ICollection<InspectionResult> Results { get; set; } = new List<InspectionResult>();
}
