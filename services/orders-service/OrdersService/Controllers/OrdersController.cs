using System.Security.Claims;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrdersService.API.Models.Requests;
using OrdersService.API.Models.Responses;
using OrdersService.Application.Interfaces;
using OrdersService.Application.Models;
using OrdersService.Domain.Entities;

namespace OrdersService.API.Controllers;

[Route("api/orders")]
[ApiController]
[Authorize(Roles = "Client")]
public class OrdersController(IOrdersService ordersService, IMapper mapper) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = mapper.Map<Order>(request);
        var created = await ordersService.CreateAsync(GetUserId(), order, cancellationToken);
        return CreatedAtAction(nameof(GetOrderById), new { id = created.Id }, mapper.Map<OrderResponse>(created));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderResponse>> GetOrderById(Guid id, CancellationToken cancellationToken)
    {
        var order = await ordersService.GetByIdAsync(GetUserId(), id, cancellationToken);
        if (order is null)
            return NotFound();

        return Ok(mapper.Map<OrderResponse>(order));
    }

    [HttpGet]
    public async Task<ActionResult<OrderListResponse>> GetOrders(
        [FromQuery] GetOrdersRequest request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await ordersService.GetByClientAsync(
            GetUserId(), request.Status, request.Page, request.PageSize, cancellationToken);

        return Ok(new OrderListResponse
        {
            Items = mapper.Map<List<OrderResponse>>(items),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
        });
    }

    [HttpPost("{id}/confirm")]
    public async Task<ActionResult<OrderResponse>> ConfirmOrder(Guid id, CancellationToken cancellationToken)
    {
        var result = await ordersService.ConfirmAsync(GetUserId(), id, cancellationToken);
        return await RespondToTransitionAsync(result, id, cancellationToken);
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelOrder(Guid id, CancellationToken cancellationToken)
    {
        var result = await ordersService.CancelAsync(GetUserId(), id, cancellationToken);

        return result switch
        {
            OrderTransitionResult.NotFound => NotFound(),
            OrderTransitionResult.Conflict => Conflict(),
            _ => NoContent(), // Success or NoChange (already cancelled) — both are "it's cancelled now".
        };
    }

    private async Task<ActionResult<OrderResponse>> RespondToTransitionAsync(
        OrderTransitionResult result,
        Guid id,
        CancellationToken cancellationToken)
    {
        if (result == OrderTransitionResult.NotFound)
            return NotFound();

        if (result == OrderTransitionResult.Conflict)
            return Conflict();

        // Success or NoChange — the transition just succeeded against this owner/id, so the
        // order is guaranteed to still exist here; the re-fetch can't actually return null.
        var order = await ordersService.GetByIdAsync(GetUserId(), id, cancellationToken);
        return Ok(mapper.Map<OrderResponse>(order!));
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Token has no 'sub' claim.");

        return Guid.Parse(userId);
    }
}
