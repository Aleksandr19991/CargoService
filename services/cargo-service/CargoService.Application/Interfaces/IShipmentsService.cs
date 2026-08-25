using CargoService.Application.Models;
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

    /// <summary>Полная карточка груза со всеми актами, услугами упаковки и историей статусов.</summary>
    Task<Shipment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Приёмка груза сотрудником: акт с состоянием упаковки/груза и фото, записи о выполненных
    /// услугах упаковки, перевод в статус <c>Accepted</c> с записью в историю. Принять можно
    /// только груз в статусе <c>Created</c> — повторная приёмка даёт <c>Conflict</c>.
    /// </summary>
    Task<ShipmentOperationResult> AcceptAsync(Guid id, ShipmentAcceptance acceptance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет идентификаторы фото к акту приёмки. Требует уже принятого груза — без акта
    /// прикладывать фото некуда, поэтому у непринятого груза возвращает <c>Conflict</c>.
    /// </summary>
    Task<ShipmentOperationResult> AddPhotosAsync(Guid id, IReadOnlyCollection<Guid> photoFileIds, CancellationToken cancellationToken = default);
}
