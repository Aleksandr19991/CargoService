using System.Security.Claims;
using CargoService.API.Models.Requests;
using CargoService.API.Models.Responses;
using CargoService.Application.Interfaces;
using CargoService.Application.Models;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CargoService.API.Controllers;

/// <summary>
/// Складская часть работы с грузом. Клиент сюда не ходит: его сценарий — публичный трекинг
/// по трек-номеру (spec.md Фаза 5) и карточка заявки в orders-service.
/// </summary>
[Route("api/shipments")]
[ApiController]
[Authorize]
public class ShipmentsController(IShipmentsService shipmentsService, IMapper mapper) : ControllerBase
{
    // Приёмка и обработка груза — работа склада; Manager/Admin допущены как надзорные роли.
    private const string StaffRoles = "WarehouseOperator,Manager,Admin";

    // Отметки статуса ставит ещё и курьер: забор и доставка — его часть маршрута. К приёмке с
    // составлением акта он при этом не допущен, поэтому роли перечислены по действиям, а не на
    // уровне класса (атрибуты класса и метода складываются через И — расширить набор на одном
    // действии иначе не получится).
    private const string StatusChangeRoles = StaffRoles + ",Courier";

    [HttpGet("{id}")]
    [Authorize(Roles = StaffRoles)]
    public async Task<ActionResult<ShipmentResponse>> GetShipmentById(Guid id, CancellationToken cancellationToken)
    {
        var shipment = await shipmentsService.GetByIdAsync(id, cancellationToken);
        if (shipment is null)
            return NotFound();

        return Ok(mapper.Map<ShipmentResponse>(shipment));
    }

    [HttpPost("{id}/accept")]
    [Authorize(Roles = StaffRoles)]
    public async Task<ActionResult<ShipmentResponse>> AcceptShipment(
        Guid id,
        [FromBody] AcceptShipmentRequest request,
        CancellationToken cancellationToken)
    {
        var acceptance = new ShipmentAcceptance
        {
            // Кто принял груз, берём из токена, а не из тела запроса — иначе сотрудник мог бы
            // подписать акт чужим именем.
            InspectedByUserId = GetUserId(),
            PackagingCondition = request.PackagingCondition,
            CargoCondition = request.CargoCondition,
            Comment = request.Comment,
            PhotoFileIds = request.PhotoFileIds,
            PerformedPackagingTypes = request.PerformedPackagingTypes,
            Location = request.Location,
        };

        var result = await shipmentsService.AcceptAsync(id, acceptance, cancellationToken);
        return await RespondWithShipmentAsync(result, id, cancellationToken);
    }

    [HttpPost("{id}/photos")]
    [Authorize(Roles = StaffRoles)]
    public async Task<ActionResult<ShipmentResponse>> AddShipmentPhotos(
        Guid id,
        [FromBody] AddShipmentPhotosRequest request,
        CancellationToken cancellationToken)
    {
        var result = await shipmentsService.AddPhotosAsync(id, request.PhotoFileIds, cancellationToken);
        return await RespondWithShipmentAsync(result, id, cancellationToken);
    }

    [HttpPost("{id}/status")]
    [Authorize(Roles = StatusChangeRoles)]
    public async Task<ActionResult<ShipmentResponse>> ChangeShipmentStatus(
        Guid id,
        [FromBody] ChangeShipmentStatusRequest request,
        CancellationToken cancellationToken)
    {
        var change = new ShipmentStatusChange
        {
            Status = request.Status,
            Location = request.Location,
            Comment = request.Comment,
        };

        var result = await shipmentsService.ChangeStatusAsync(id, change, cancellationToken);
        return await RespondWithShipmentAsync(result, id, cancellationToken);
    }

    private async Task<ActionResult<ShipmentResponse>> RespondWithShipmentAsync(
        ShipmentOperationResult result,
        Guid id,
        CancellationToken cancellationToken)
    {
        if (result == ShipmentOperationResult.NotFound)
            return NotFound();

        if (result == ShipmentOperationResult.Conflict)
            return Conflict();

        // Операция только что отработала по этому id, так что груз гарантированно существует —
        // перечитывание не может вернуть null.
        var shipment = await shipmentsService.GetByIdAsync(id, cancellationToken);
        return Ok(mapper.Map<ShipmentResponse>(shipment!));
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Token has no 'sub' claim.");

        return Guid.Parse(userId);
    }
}
