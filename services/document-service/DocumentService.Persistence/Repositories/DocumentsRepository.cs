using DocumentService.Application.Interfaces;
using DocumentService.Domain.Entities;
using DocumentService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocumentService.Persistence.Repositories;

public class DocumentsRepository(AppDbContext dbContext) : IDocumentsRepository
{
    public async Task AddRangeAsync(IReadOnlyList<Document> documents, CancellationToken cancellationToken)
    {
        await dbContext.Documents.AddRangeAsync(documents, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Захват документа сделан одним SQL с <c>FOR UPDATE SKIP LOCKED</c>, а не «прочитали →
    /// обновили»: второй способ отдал бы один документ двум экземплярам сервиса, и в хранилище
    /// легли бы два одинаковых PDF с разными идентификаторами. Строка сразу переводится в
    /// <c>Processing</c> и тем самым выходит из выборки непечатанных.
    /// </summary>
    public async Task<Document?> ClaimNextPendingAsync(CancellationToken cancellationToken)
    {
        var documents = await dbContext.Documents
            .FromSql(
                $"""
                 UPDATE documents
                 SET "Status" = 'Processing'
                 WHERE "Id" = (
                     SELECT "Id" FROM documents
                     WHERE "Status" = 'Pending'
                     ORDER BY "CreatedAt"
                     FOR UPDATE SKIP LOCKED
                     LIMIT 1
                 )
                 RETURNING *
                 """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return documents.FirstOrDefault();
    }

    public async Task MarkReadyAsync(Guid documentId, Guid fileId, CancellationToken cancellationToken)
    {
        var document = await dbContext.Documents.FirstAsync(stored => stored.Id == documentId, cancellationToken);

        document.Status = DocumentStatus.Ready;
        document.FileId = fileId;
        document.GeneratedAt = DateTimeOffset.UtcNow;

        // SaveChanges здесь один на всё: событие в outbox поставлено вызывающим кодом на этом же
        // DbContext, поэтому статус, ссылка на файл и событие коммитятся одной транзакцией.
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(Guid documentId, string reason, CancellationToken cancellationToken)
    {
        var document = await dbContext.Documents.FirstAsync(stored => stored.Id == documentId, cancellationToken);

        document.Status = DocumentStatus.Failed;
        document.FailureReason = reason;
        document.GeneratedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
