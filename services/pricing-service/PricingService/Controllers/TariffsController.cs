using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PricingService.API.Models.Requests;
using PricingService.API.Models.Responses;
using PricingService.Application.Interfaces;

namespace PricingService.API.Controllers;

[Route("api/tariffs")]
[ApiController]
[Authorize(Roles = "Admin,Manager")]
public class TariffsController(ITariffsService tariffsService, IMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TariffRateResponse>>> GetAllTariffs(CancellationToken cancellationToken)
    {
        var tariffs = await tariffsService.GetAllCurrentAsync(cancellationToken);
        return Ok(mapper.Map<List<TariffRateResponse>>(tariffs));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TariffRateResponse>> UpdateTariff(
        Guid id,
        [FromBody] UpdateTariffRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await tariffsService.UpdatePriceAsync(id, request.Price, cancellationToken);
        if (updated is null)
            return NotFound();

        return Ok(mapper.Map<TariffRateResponse>(updated));
    }
}
