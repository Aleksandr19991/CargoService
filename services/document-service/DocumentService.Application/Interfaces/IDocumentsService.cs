using DocumentService.Application.Models;
using DocumentService.Domain.Entities;

namespace DocumentService.Application.Interfaces;

public interface IDocumentsService
{
    /// <summary>
    /// Заводит документы, которые полагаются при приёмке груза. Возвращает заведённое — чтобы
    /// вызывающий мог сказать в лог, что именно выпущено.
    /// </summary>
    Task<IReadOnlyList<Document>> RegisterForAcceptanceAsync(
        CargoAcceptanceFacts facts,
        CancellationToken cancellationToken);

    /// <summary>Заводит документы, которые полагаются при выдаче груза получателю.</summary>
    Task<IReadOnlyList<Document>> RegisterForDeliveryAsync(
        CargoDeliveryFacts facts,
        CancellationToken cancellationToken);
}
