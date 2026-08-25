using CargoService.Application.Interfaces;
using CargoService.Domain.Entities;
using CargoService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CargoService.Persistence.Repositories;

public class ShipmentsRepository(AppDbContext context) : IShipmentsRepository
{
    // 23505 = unique_violation. Сверяемся с кодом, а не с текстом сообщения, чтобы не зависеть
    // от локали сервера БД.
    private const string UniqueViolationSqlState = "23505";

    public async Task<bool> ExistsByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await context.Shipments
            .AsNoTracking()
            .AnyAsync(shipment => shipment.OrderId == orderId, cancellationToken);
    }

    public async Task<Shipment?> TryCreateAsync(Shipment shipment, CancellationToken cancellationToken = default)
    {
        // Вложенные StatusHistory/Inspections/PackagingServices сохраняются тем же вызовом —
        // EF обходит граф от корня.
        context.Shipments.Add(shipment);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return shipment;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState })
        {
            // Сюда попадаем только на гонке: обычный дубликат отсекается проверкой
            // ExistsByOrderIdAsync до вставки. Это важно не ради скорости, а ради логов — EF на
            // каждом неудавшемся SaveChanges пишет два ERR со стектрейсом (проверено живым
            // прогоном), и без предварительной проверки пачка переотправленных событий залила бы
            // Seq ложными ошибками.
            context.Entry(shipment).State = EntityState.Detached;
            return null;
        }
    }

    public async Task<Shipment?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Без AsNoTracking: приёмка и добавление фото мутируют этот же экземпляр и сохраняют его
        // голым SaveChangesAsync (как CounterpartiesRepository в clients-service).
        return await context.Shipments
            .Include(shipment => shipment.Inspections)
            .Include(shipment => shipment.PackagingServices)
            .Include(shipment => shipment.StatusHistory)
            .FirstOrDefaultAsync(shipment => shipment.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(Shipment shipment, CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRangeAsync(IReadOnlyCollection<Shipment> shipments, CancellationToken cancellationToken = default)
    {
        // Все грузы пачки отслеживаются одним контекстом, поэтому один SaveChanges сохраняет их
        // все в одной транзакции — отдельный метод существует ради читаемости вызова.
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Shipment?> GetByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        // Только чтение — отслеживать нечего.
        return await context.Shipments
            .AsNoTracking()
            .Include(shipment => shipment.StatusHistory)
            .FirstOrDefaultAsync(shipment => shipment.TrackingNumber == trackingNumber, cancellationToken);
    }

    public async Task<List<Shipment>> GetOverdueAsync(
        DateTimeOffset asOf,
        IReadOnlyCollection<ShipmentStatus> eligibleStatuses,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        // Отслеживаемые (без AsNoTracking): вызывающий меняет статус и дописывает историю.
        // Пачками, чтобы одна просроченная тысяча не превратилась в один гигантский SaveChanges.
        return await context.Shipments
            .Include(shipment => shipment.StatusHistory)
            .Where(shipment => shipment.DeliveryDeadline != null
                && shipment.DeliveryDeadline < asOf
                && eligibleStatuses.Contains(shipment.CurrentStatus))
            .OrderBy(shipment => shipment.DeliveryDeadline)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }
}
