using DocumentService.Application.Interfaces;
using DocumentService.Application.Models;
using DocumentService.Domain.Entities;
using DocumentService.Domain.Enums;

namespace DocumentService.Application;

/// <summary>
/// Решает, какие документы полагаются по событию груза, и заводит их записи. Сама печать —
/// дело фонового рабочего (задача 5 Фазы 8).
/// </summary>
public class DocumentsService(IDocumentsRepository documentsRepository) : IDocumentsService
{
    /// <summary>
    /// Состояние, при котором груз считается целым. Сравнение со строкой, а не с чужим enum:
    /// значения приходят из cargo-service, и типа его состояний здесь нет. Всё, что не «Intact»,
    /// считается поводом для акта осмотра — в том числе значение, которого мы ещё не знаем
    /// (cargo-service обещает расширять эти перечисления): лишний акт лучше пропущенного.
    /// </summary>
    private const string IntactCondition = "Intact";

    public Task<IReadOnlyList<Document>> RegisterForAcceptanceAsync(
        CargoAcceptanceFacts facts,
        CancellationToken cancellationToken)
    {
        // Накладная и акт приёма-передачи выпускаются на приёмке: с этого момента груз едет с
        // сопроводительными документами, и оба нужны независимо от его состояния.
        var documents = new List<Document>
        {
            Create(DocumentType.Waybill, facts),
            Create(DocumentType.AcceptanceAct, facts),
        };

        if (IsDamaged(facts.PackagingCondition) || IsDamaged(facts.CargoCondition))
            documents.Add(Create(DocumentType.DamageInspectionAct, facts));

        return SaveAsync(documents, cancellationToken);
    }

    public Task<IReadOnlyList<Document>> RegisterForDeliveryAsync(
        CargoDeliveryFacts facts,
        CancellationToken cancellationToken)
    {
        // При выдаче нужен акт приёма-передачи — второй, на другую передачу груза: первый
        // подтверждает, что груз принял перевозчик, этот — что его получил адресат. Отличаются
        // они данными, а не видом документа, поэтому это тот же DocumentType.
        var document = new Document
        {
            Id = Guid.NewGuid(),
            Type = DocumentType.AcceptanceAct,
            Status = DocumentStatus.Pending,
            ShipmentId = facts.ShipmentId,
            OrderId = facts.OrderId,
            TrackingNumber = facts.TrackingNumber,
            IssuedAt = facts.DeliveredAt,
            ReceivedByName = facts.ReceivedByName,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        return SaveAsync([document], cancellationToken);
    }

    private async Task<IReadOnlyList<Document>> SaveAsync(
        List<Document> documents,
        CancellationToken cancellationToken)
    {
        await documentsRepository.AddRangeAsync(documents, cancellationToken);

        return documents;
    }

    private static Document Create(DocumentType type, CargoAcceptanceFacts facts) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        Status = DocumentStatus.Pending,
        ShipmentId = facts.ShipmentId,
        OrderId = facts.OrderId,
        TrackingNumber = facts.TrackingNumber,
        IssuedAt = facts.AcceptedAt,
        PackagingCondition = facts.PackagingCondition,
        CargoCondition = facts.CargoCondition,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static bool IsDamaged(string condition) =>
        !string.Equals(condition, IntactCondition, StringComparison.OrdinalIgnoreCase);
}
