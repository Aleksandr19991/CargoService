using DocumentService.Application.Interfaces;
using DocumentService.Domain.Entities;

namespace DocumentService.Application;

public class DocumentsQueryService(
    IDocumentsRepository documentsRepository,
    IOrderSnapshotsRepository snapshotsRepository) : IDocumentsQueryService
{
    public async Task<IReadOnlyList<Document>> GetByOrderAsync(
        Guid orderId,
        DocumentAccess access,
        CancellationToken cancellationToken)
    {
        if (!await IsAllowedAsync(orderId, access, cancellationToken))
            return [];

        return await documentsRepository.GetByOrderAsync(orderId, cancellationToken);
    }

    public async Task<IReadOnlyList<Document>> GetByShipmentAsync(
        Guid shipmentId,
        DocumentAccess access,
        CancellationToken cancellationToken)
    {
        var documents = await documentsRepository.GetByShipmentAsync(shipmentId, cancellationToken);
        if (documents.Count == 0 || access.IsStaff)
            return documents;

        // У всех документов груза одна заявка, поэтому владение проверяется один раз.
        return await IsAllowedAsync(documents[0].OrderId, access, cancellationToken) ? documents : [];
    }

    public async Task<Document?> GetByIdAsync(Guid documentId, DocumentAccess access, CancellationToken cancellationToken)
    {
        var document = await documentsRepository.GetByIdAsync(documentId, cancellationToken);
        if (document is null)
            return null;

        return await IsAllowedAsync(document.OrderId, access, cancellationToken) ? document : null;
    }

    /// <summary>
    /// Клиенту доступны документы только по его заявкам. Если сведений о заявке ещё нет
    /// (событие <c>OrderCreated</c> не обработано), доступа тоже нет: проверить владение нечем,
    /// а показать документ на всякий случай — значит показать его чужому.
    /// </summary>
    private async Task<bool> IsAllowedAsync(Guid orderId, DocumentAccess access, CancellationToken cancellationToken)
    {
        if (access.IsStaff)
            return true;

        var snapshot = await snapshotsRepository.GetAsync(orderId, cancellationToken);

        return snapshot is not null && snapshot.ClientAccountId == access.RequesterId;
    }
}
