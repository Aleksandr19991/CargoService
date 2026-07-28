using MapsterMapper;
using Microsoft.AspNetCore.Mvc;
using PricingService.API.Models.Requests;
using PricingService.API.Models.Responses;
using PricingService.Application.Interfaces;
using PricingService.Application.Models;

namespace PricingService.API.Controllers;

// Public by construction — no authentication is wired up in this service at all yet (see
// CLAUDE.md/spec.md §3.7: the price calculator is meant to be usable from the public website
// without creating an order or logging in).
[Route("api/pricing")]
[ApiController]
public class PricingController(IPricingCalculationService pricingCalculationService, IMapper mapper) : ControllerBase
{
    [HttpPost("calculate")]
    public async Task<ActionResult<PriceCalculationResponse>> Calculate(
        [FromBody] PriceCalculationRequest request,
        CancellationToken cancellationToken)
    {
        var input = mapper.Map<PriceCalculationInput>(request);
        var result = await pricingCalculationService.CalculateAsync(input, cancellationToken);
        return Ok(mapper.Map<PriceCalculationResponse>(result));
    }
}
