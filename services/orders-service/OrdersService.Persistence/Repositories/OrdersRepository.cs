using OrdersService.Application.Interfaces;
using OrdersService.Domain.Entities;
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
}
