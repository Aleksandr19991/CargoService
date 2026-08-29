using DocumentService.Domain.Enums;

namespace DocumentService.Domain.Entities;

/// <summary>
/// Документ по грузу: сначала запись о том, что он нужен, потом — ссылка на готовый PDF.
/// <para>
/// Консьюмеры событий только заводят такие записи, а собирает и сохраняет их фоновый рабочий
/// (задача 5 Фазы 8): вёрстка PDF и загрузка файла в хранилище — работа на секунды, и держать
/// на неё открытым сообщение брокера значило бы мерить его таймауты скоростью печати. Тот же
/// приём, что у ai-inspection-service с заданиями на инференс.
/// </para>
/// <para>
/// Обстоятельства приёмки и выдачи хранятся прямо здесь, а не берутся при печати из чужого
/// сервиса: документ обязан отражать состояние на момент события, а не то, что стало потом.
/// </para>
/// </summary>
public class Document
{
    public Guid Id { get; set; }

    public DocumentType Type { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

    // References Shipment.Id / Order.Id in other services — без FK и навигации, «database per service».
    public Guid ShipmentId { get; set; }
    public Guid OrderId { get; set; }

    public string TrackingNumber { get; set; } = string.Empty;

    /// <summary>Момент события, по которому документ понадобился, — он же дата в бланке.</summary>
    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>Состояния из акта приёмки; у документов выдачи их нет.</summary>
    public string? PackagingCondition { get; set; }
    public string? CargoCondition { get; set; }

    /// <summary>Кому выдан груз — из события выдачи.</summary>
    public string? ReceivedByName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? GeneratedAt { get; set; }

    /// <summary>Файл в file-storage-service; появляется вместе со статусом <c>Ready</c>.</summary>
    public Guid? FileId { get; set; }

    public string? FailureReason { get; set; }
}
