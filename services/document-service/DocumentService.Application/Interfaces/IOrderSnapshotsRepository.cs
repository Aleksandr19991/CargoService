using DocumentService.Domain.Entities;

namespace DocumentService.Application.Interfaces;

public interface IOrderSnapshotsRepository
{
    Task<OrderSnapshot?> GetAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Создаёт или обновляет сведения о заявке (событие может прийти повторно).</summary>
    Task UpsertAsync(OrderSnapshot snapshot, CancellationToken cancellationToken);

    /// <summary>
    /// Дописывает стоимость и срок из <c>OrderConfirmed</c>. Отдельно от полного upsert, потому
    /// что подтверждение приходит после создания и не несёт остальных полей — перезаписать ими
    /// снимок значило бы стереть стороны и характеристики груза.
    /// </summary>
    Task UpdatePriceAsync(
        Guid orderId,
        decimal calculatedPrice,
        DateTimeOffset? deliveryDeadline,
        CancellationToken cancellationToken);
}
