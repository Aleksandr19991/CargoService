using DocumentService.Domain.Entities;

namespace DocumentService.Application.Interfaces;

public interface IDocumentsRepository
{
    /// <summary>Заводит документы одним заходом: комплект по одному событию пишется вместе.</summary>
    Task AddRangeAsync(IReadOnlyList<Document> documents, CancellationToken cancellationToken);

    /// <summary>
    /// Берёт самый старый непечатанный документ и сразу помечает его в работе — одной
    /// операцией, чтобы два экземпляра сервиса не напечатали один документ дважды.
    /// <c>null</c>, если печатать нечего.
    /// </summary>
    Task<Document?> ClaimNextPendingAsync(CancellationToken cancellationToken);

    /// <summary>Закрывает документ: ссылка на файл, статус и событие в outbox — одной транзакцией.</summary>
    Task MarkReadyAsync(Guid documentId, Guid fileId, CancellationToken cancellationToken);

    Task MarkFailedAsync(Guid documentId, string reason, CancellationToken cancellationToken);
}
