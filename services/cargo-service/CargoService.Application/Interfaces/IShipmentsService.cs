using CargoService.Domain.Entities;

namespace CargoService.Application.Interfaces;

public interface IShipmentsService
{
    /// <summary>
    /// Заводит груз по подтверждённой заявке: генерирует трек-номер, ставит статус
    /// <c>Created</c> и первую запись в историю статусов. Идемпотентна — возвращает
    /// <c>null</c>, если груз по этой заявке уже существует (повторная доставка OrderConfirmed).
    /// </summary>
    Task<Shipment?> CreateFromConfirmedOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
}
