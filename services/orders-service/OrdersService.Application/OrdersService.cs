using System.Security.Cryptography;
using OrdersService.Application.Interfaces;
using OrdersService.Application.Models;
using OrdersService.Domain.Entities;
using OrdersService.Domain.Enums;

namespace OrdersService.Application;

public class OrdersService(
    IOrdersRepository ordersRepository,
    IPricingClient pricingClient) : IOrdersService
{
    private const string NumberAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I — avoids visual ambiguity

    public async Task<Order> CreateAsync(Guid userId, Order order, CancellationToken cancellationToken = default)
    {
        order.ClientAccountId = userId;
        order.Number = GenerateNumber();
        order.Status = OrderStatus.Created;
        order.CreatedAt = DateTimeOffset.UtcNow;

        var priceRequest = new PriceCalculationRequest
        {
            WeightKg = order.CargoWeight,
            VolumeM3 = order.CargoVolumeM3,
            DistanceKm = order.DistanceKm,
            ShippingType = order.ServiceOptions.ShippingType,
            PackagingType = order.ServiceOptions.PackagingType,
            NeedsPickup = order.ServiceOptions.NeedsPickup,
            NeedsDelivery = order.ServiceOptions.NeedsDelivery,
            NeedsInsurance = order.ServiceOptions.NeedsInsurance,
            DeclaredValue = order.DeclaredValue,
        };
        var priceResult = await pricingClient.CalculateAsync(priceRequest, cancellationToken);
        order.CalculatedPrice = priceResult.TotalPrice;

        return await ordersRepository.CreateAsync(order, cancellationToken);
    }

    public Task<Order?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        return ordersRepository.GetByIdAsync(id, userId, cancellationToken);
    }

    public Task<OrderTransitionResult> ConfirmAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        return TransitionAsync(userId, id, OrderStatus.Created, OrderStatus.Confirmed, cancellationToken);
    }

    public async Task<OrderTransitionResult> CancelAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var order = await ordersRepository.GetByIdAsync(id, userId, cancellationToken);
        if (order is null)
            return OrderTransitionResult.NotFound;

        if (order.Status == OrderStatus.Cancelled)
            return OrderTransitionResult.NoChange;

        // Both Created and Confirmed orders can still be cancelled — only an already-cancelled
        // order rejects the transition (handled above).
        order.Status = OrderStatus.Cancelled;
        await ordersRepository.UpdateAsync(order, cancellationToken);
        return OrderTransitionResult.Success;
    }

    private async Task<OrderTransitionResult> TransitionAsync(
        Guid userId,
        Guid id,
        OrderStatus from,
        OrderStatus to,
        CancellationToken cancellationToken)
    {
        var order = await ordersRepository.GetByIdAsync(id, userId, cancellationToken);
        if (order is null)
            return OrderTransitionResult.NotFound;

        if (order.Status == to)
            return OrderTransitionResult.NoChange;

        if (order.Status != from)
            return OrderTransitionResult.Conflict;

        order.Status = to;
        await ordersRepository.UpdateAsync(order, cancellationToken);
        return OrderTransitionResult.Success;
    }

    private static string GenerateNumber()
    {
        Span<char> suffix = stackalloc char[6];
        for (var i = 0; i < suffix.Length; i++)
            suffix[i] = NumberAlphabet[RandomNumberGenerator.GetInt32(NumberAlphabet.Length)];

        return $"{DateTimeOffset.UtcNow:yyyyMMdd}-{new string(suffix)}";
    }
}
