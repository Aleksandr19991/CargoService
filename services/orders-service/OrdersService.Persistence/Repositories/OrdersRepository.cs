using OrdersService.Application.Interfaces;
using OrdersService.Domain.Entities;
using OrdersService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace OrdersService.Persistence.Repositories;

public class OrdersRepository(AppDbContext context) : IOrdersRepository
{
    public async Task<Order> CreateAsync(Order order, CancellationToken cancellationToken = default)
    {
        context.Orders.Add(order);
        await context.SaveChangesAsync(cancellationToken);
        return order;
    }

    public async Task<Order?> GetByIdAsync(Guid id, Guid clientAccountId, CancellationToken cancellationToken = default)
    {
        return await context.Orders
            .FirstOrDefaultAsync(order => order.Id == id && order.ClientAccountId == clientAccountId, cancellationToken);
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Orders.FirstOrDefaultAsync(order => order.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<(List<Order> Items, int TotalCount)> GetByClientAsync(
        Guid clientAccountId,
        OrderStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.Orders.Where(order => order.ClientAccountId == clientAccountId);
        if (status.HasValue)
            query = query.Where(order => order.Status == status.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(order => order.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
