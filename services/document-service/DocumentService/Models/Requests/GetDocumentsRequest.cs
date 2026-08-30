namespace DocumentService.API.Models.Requests;

/// <summary>
/// Документы спрашивают либо по заявке, либо по грузу — ровно по одному из двух: клиент знает
/// номер заявки, склад работает с трек-номером и грузом, а «по обоим сразу» означало бы
/// пересечение двух разных вопросов в одном ответе.
/// </summary>
public sealed record GetDocumentsRequest
{
    public Guid? OrderId { get; init; }
    public Guid? ShipmentId { get; init; }
}
