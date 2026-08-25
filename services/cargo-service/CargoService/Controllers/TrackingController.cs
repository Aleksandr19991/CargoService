using CargoService.API.Models.Responses;
using CargoService.Application.Interfaces;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CargoService.API.Controllers;

/// <summary>
/// Публичный трекинг груза. Единственный анонимный эндпоинт сервиса — клиенту достаточно знать
/// трек-номер, регистрироваться не нужно. Набор полей урезан, см. <see cref="ShipmentTrackingResponse"/>.
/// </summary>
[Route("api/track")]
[ApiController]
[AllowAnonymous]
public class TrackingController(IShipmentsService shipmentsService, IMapper mapper) : ControllerBase
{
    [HttpGet("{trackingNumber}")]
    public async Task<ActionResult<ShipmentTrackingResponse>> TrackShipment(
        string trackingNumber,
        CancellationToken cancellationToken)
    {
        var shipment = await shipmentsService.GetByTrackingNumberAsync(trackingNumber, cancellationToken);
        if (shipment is null)
            return NotFound();

        return Ok(mapper.Map<ShipmentTrackingResponse>(shipment));
    }
}
