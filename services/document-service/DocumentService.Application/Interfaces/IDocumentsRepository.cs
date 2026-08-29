using DocumentService.Domain.Entities;

namespace DocumentService.Application.Interfaces;

public interface IDocumentsRepository
{
    /// <summary>Заводит документы одним заходом: комплект по одному событию пишется вместе.</summary>
    Task AddRangeAsync(IReadOnlyList<Document> documents, CancellationToken cancellationToken);
}
