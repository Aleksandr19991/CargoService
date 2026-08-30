using DocumentService.Domain.Enums;

namespace DocumentService.API.Models.Responses;

/// <summary>
/// Документ в списке. Идентификатор файла отдаётся, но ссылку на скачивание нужно запрашивать
/// отдельно: она подписана и живёт минуты, поэтому в списке успела бы протухнуть раньше, чем по
/// ней кликнут.
/// </summary>
public sealed record DocumentResponse
{
    public required Guid Id { get; init; }
    public required DocumentType Type { get; init; }
    public required DocumentStatus Status { get; init; }
    public required Guid OrderId { get; init; }
    public required Guid ShipmentId { get; init; }
    public required string TrackingNumber { get; init; }
    public required DateTimeOffset IssuedAt { get; init; }
    public DateTimeOffset? GeneratedAt { get; init; }
    public Guid? FileId { get; init; }

    /// <summary>Причина, по которой документ не удалось сформировать. Только при статусе <c>Failed</c>.</summary>
    public string? FailureReason { get; init; }
}
