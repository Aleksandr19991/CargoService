using OrdersService.Domain.Enums;

namespace OrdersService.API.Models.Requests;

public sealed record GetOrdersRequest
{
    public OrderStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
