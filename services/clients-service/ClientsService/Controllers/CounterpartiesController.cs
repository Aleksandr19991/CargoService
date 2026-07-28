using System.Security.Claims;
using ClientsService.API.Models.Requests;
using ClientsService.API.Models.Responses;
using ClientsService.Application.Interfaces;
using ClientsService.Domain.Entities;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClientsService.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Client")]
public class CounterpartiesController(ICounterpartiesService counterpartiesService, IMapper mapper) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CounterpartyResponse>> CreateCounterparty(
        [FromBody] CreateCounterpartyRequest request,
        CancellationToken cancellationToken)
    {
        var counterparty = mapper.Map<Counterparty>(request);
        var created = await counterpartiesService.CreateAsync(GetUserId(), counterparty, cancellationToken);
        return CreatedAtAction(nameof(GetCounterpartyById), new { id = created.Id }, mapper.Map<CounterpartyResponse>(created));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CounterpartyResponse>> GetCounterpartyById(Guid id, CancellationToken cancellationToken)
    {
        var counterparty = await counterpartiesService.GetByIdAsync(GetUserId(), id, cancellationToken);
        if (counterparty is null)
            return NotFound();

        return Ok(mapper.Map<CounterpartyResponse>(counterparty));
    }

    [HttpGet]
    public async Task<ActionResult<List<CounterpartyResponse>>> SearchCounterparties(
        [FromQuery] string? city,
        [FromQuery] string? name,
        [FromQuery] string? phone,
        CancellationToken cancellationToken)
    {
        var counterparties = await counterpartiesService.SearchAsync(GetUserId(), city, name, phone, cancellationToken);
        return Ok(mapper.Map<List<CounterpartyResponse>>(counterparties));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCounterparty(
        Guid id,
        [FromBody] UpdateCounterpartyRequest request,
        CancellationToken cancellationToken)
    {
        var counterparty = mapper.Map<Counterparty>(request);
        var updated = await counterpartiesService.UpdateAsync(GetUserId(), id, counterparty, cancellationToken);
        if (!updated)
            return NotFound();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCounterparty(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await counterpartiesService.DeleteAsync(GetUserId(), id, cancellationToken);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Token has no 'sub' claim.");

        return Guid.Parse(userId);
    }
}
