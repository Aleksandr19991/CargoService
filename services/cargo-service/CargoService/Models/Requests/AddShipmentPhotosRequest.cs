namespace CargoService.API.Models.Requests;

public sealed record AddShipmentPhotosRequest
{
    /// <summary>Идентификаторы файлов, уже загруженных в File Storage (Фаза 10).</summary>
    public required List<Guid> PhotoFileIds { get; init; }
}
