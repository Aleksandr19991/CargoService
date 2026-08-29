using DocumentService.Application.Interfaces;
using DocumentService.Domain.Entities;

namespace DocumentService.Persistence.Repositories;

public class DocumentsRepository(AppDbContext dbContext) : IDocumentsRepository
{
    public async Task AddRangeAsync(IReadOnlyList<Document> documents, CancellationToken cancellationToken)
    {
        await dbContext.Documents.AddRangeAsync(documents, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
