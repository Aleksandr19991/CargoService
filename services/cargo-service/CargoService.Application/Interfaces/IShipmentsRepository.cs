using CargoService.Domain.Entities;

namespace CargoService.Application.Interfaces;

public interface IShipmentsRepository
{
    /// <summary>Есть ли уже груз по этой заявке (см. <see cref="TryCreateAsync"/> о том, зачем нужны обе проверки).</summary>
    Task<bool> ExistsByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет груз вместе с вложенной историей статусов. Возвращает <c>null</c>, если груз по
    /// этому <c>OrderId</c> уже существует: уникальный индекс — последний рубеж, который ловит
    /// гонку двух одновременных доставок OrderConfirmed (RabbitMQ at-least-once), когда обе
    /// успели пройти <see cref="ExistsByOrderIdAsync"/> до вставки.
    /// </summary>
    Task<Shipment?> TryCreateAsync(Shipment shipment, CancellationToken cancellationToken = default);

    /// <summary>Груз со всеми дочерними коллекциями. Отслеживаемый — вызывающий мутирует его и сохраняет через <see cref="UpdateAsync"/>.</summary>
    Task<Shipment?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpdateAsync(Shipment shipment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Груз по трек-номеру вместе с историей статусов — для публичного трекинга. Акты приёмки и
    /// услуги упаковки не подтягиваются: наружу они всё равно не отдаются.
    /// </summary>
    Task<Shipment?> GetByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken = default);
}
