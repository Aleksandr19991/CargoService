using DocumentService.Application.Interfaces;
using DocumentService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DocumentService.Persistence.Repositories;

public class OrderSnapshotsRepository(AppDbContext dbContext) : IOrderSnapshotsRepository
{
    // 23505 = unique_violation. Сверяемся с кодом, а не с текстом сообщения, чтобы не зависеть
    // от локали и версии сервера.
    private const string UniqueViolationSqlState = "23505";

    public Task<OrderSnapshot?> GetAsync(Guid orderId, CancellationToken cancellationToken) =>
        dbContext.OrderSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(snapshot => snapshot.OrderId == orderId, cancellationToken);

    public Task UpsertAsync(OrderSnapshot snapshot, CancellationToken cancellationToken) =>
        ApplyAsync(
            snapshot.OrderId,
            stored =>
            {
                stored.ClientAccountId = snapshot.ClientAccountId;
                stored.OrderNumber = snapshot.OrderNumber;
                stored.OriginCity = snapshot.OriginCity;
                stored.DestinationCity = snapshot.DestinationCity;
                stored.SenderName = snapshot.SenderName;
                stored.RecipientName = snapshot.RecipientName;
                stored.CargoName = snapshot.CargoName;
                stored.CargoWeightKg = snapshot.CargoWeightKg;
                stored.CargoVolumeM3 = snapshot.CargoVolumeM3;
                stored.DeclaredValue = snapshot.DeclaredValue;

                // Стоимость и срок здесь не трогаются: они приезжают отдельным событием, и
                // повторная доставка OrderCreated не должна их стирать.
            },
            cancellationToken);

    public Task UpdatePriceAsync(
        Guid orderId,
        decimal calculatedPrice,
        DateTimeOffset? deliveryDeadline,
        CancellationToken cancellationToken) =>
        ApplyAsync(
            orderId,
            stored =>
            {
                stored.CalculatedPrice = calculatedPrice;
                stored.DeliveryDeadline = deliveryDeadline;
            },
            cancellationToken);

    /// <summary>
    /// Правит строку заявки, создавая её при необходимости.
    /// <para>
    /// Вставка защищена от гонки: <c>OrderCreated</c> и <c>OrderConfirmed</c> обрабатываются
    /// разными консьюмерами параллельно, и на новой заявке оба видят пустоту и оба вставляют —
    /// проверено живым прогоном, где второе событие уходило в DLQ по нарушению первичного
    /// ключа. Поймав 23505, перечитываем строку и дописываем в неё своё: событие обязано
    /// применить свои поля, а не откатиться из-за того, что соседнее успело раньше.
    /// </para>
    /// <para>
    /// Подтверждение может обогнать создание (или сервис подключили к шине позже) — тогда
    /// строка заводится по нему, а остальные поля допишет <c>OrderCreated</c>, если ещё придёт.
    /// Печать неполной накладной лучше её отсутствия: пустые поля видны прочерком.
    /// </para>
    /// </summary>
    private async Task ApplyAsync(Guid orderId, Action<OrderSnapshot> apply, CancellationToken cancellationToken)
    {
        var existing = await dbContext.OrderSnapshots
            .FirstOrDefaultAsync(stored => stored.OrderId == orderId, cancellationToken);

        if (existing is not null)
        {
            apply(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var created = new OrderSnapshot { OrderId = orderId };
        apply(created);

        try
        {
            await dbContext.OrderSnapshots.AddAsync(created, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState })
        {
            dbContext.ChangeTracker.Clear();

            var concurrent = await dbContext.OrderSnapshots
                .FirstAsync(stored => stored.OrderId == orderId, cancellationToken);

            apply(concurrent);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
