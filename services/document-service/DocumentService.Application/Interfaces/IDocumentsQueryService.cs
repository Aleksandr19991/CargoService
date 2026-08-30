using DocumentService.Domain.Entities;

namespace DocumentService.Application.Interfaces;

/// <summary>
/// Выдача документов клиенту и сотрудникам. Права проверяются здесь, а не в контроллере: правило
/// «клиент видит только документы по своим заявкам» опирается на данные (владельца заявки из
/// read-модели), и держать его в контроллере значило бы разнести проверку и её основание.
/// </summary>
public interface IDocumentsQueryService
{
    Task<IReadOnlyList<Document>> GetByOrderAsync(
        Guid orderId,
        DocumentAccess access,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Document>> GetByShipmentAsync(
        Guid shipmentId,
        DocumentAccess access,
        CancellationToken cancellationToken);

    /// <summary>Документ по идентификатору; <c>null</c>, если его нет или он не доступен запросившему.</summary>
    Task<Document?> GetByIdAsync(Guid documentId, DocumentAccess access, CancellationToken cancellationToken);
}

/// <summary>
/// Кто спрашивает документы. Сотрудник видит все, клиент — только по своим заявкам; сам факт
/// «клиент» и его идентификатор приходят из токена.
/// </summary>
public sealed record DocumentAccess
{
    public required bool IsStaff { get; init; }
    public required Guid RequesterId { get; init; }

    public static DocumentAccess Staff(Guid userId) => new() { IsStaff = true, RequesterId = userId };

    public static DocumentAccess Client(Guid userId) => new() { IsStaff = false, RequesterId = userId };
}
