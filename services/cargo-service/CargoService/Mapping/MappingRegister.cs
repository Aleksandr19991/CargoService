using CargoService.API.Models.Responses;
using CargoService.Domain.Entities;
using Mapster;

namespace CargoService.API.Mapping;

/// <summary>
/// Every request/response DTO ↔ domain entity mapping lives here, not in controllers. Discovered
/// and applied automatically by <c>services.AddMapster()</c> in Program.cs (Mapster scans the
/// assembly for <see cref="IRegister"/> implementations).
/// </summary>
public class MappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<AcceptanceInspection, AcceptanceInspectionResponse>();
        config.NewConfig<PackagingService, PackagingServiceResponse>();
        config.NewConfig<ShipmentStatusHistory, ShipmentStatusHistoryResponse>();

        // История отдаётся в хронологическом порядке — в сущности это обычная коллекция без
        // гарантий сортировки, а клиенту карточки нужна лента сверху вниз.
        config.NewConfig<Shipment, ShipmentResponse>()
            .Map(dest => dest.StatusHistory, src => src.StatusHistory.OrderBy(history => history.ChangedAt));

        config.NewConfig<ShipmentStatusHistory, ShipmentTrackingHistoryResponse>();

        // Публичный трекинг: перечисляем поля поимённо, а не полагаемся на совпадение имён.
        // Так добавление поля в Shipment не утечёт наружу само собой — что для анонимного
        // эндпоинта важнее краткости (список исключённого — в докблоке ShipmentTrackingResponse).
        config.NewConfig<Shipment, ShipmentTrackingResponse>()
            .Map(dest => dest.TrackingNumber, src => src.TrackingNumber)
            .Map(dest => dest.CurrentStatus, src => src.CurrentStatus)
            .Map(dest => dest.CreatedAt, src => src.CreatedAt)
            .Map(dest => dest.History, src => src.StatusHistory.OrderBy(history => history.ChangedAt))
            .IgnoreNonMapped(true);

        // AcceptShipmentRequest → ShipmentAcceptance намеренно собирается вручную в контроллере:
        // InspectedByUserId берётся из токена, а не из тела запроса, и маппер про него не знает.
    }
}
