using CargoService.Application.Interfaces;
using CargoService.Domain.Entities;
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
}
