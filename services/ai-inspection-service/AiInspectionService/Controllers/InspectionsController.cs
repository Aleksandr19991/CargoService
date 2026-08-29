using AiInspectionService.API.Models.Requests;
using AiInspectionService.API.Models.Responses;
using AiInspectionService.Application.Interfaces;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiInspectionService.API.Controllers;

/// <summary>
/// Проверки целостности упаковки (spec.md §2.7).
/// <para>
/// Роли — сотрудники склада и надзорные: обычный путь проверки автоматический (событие
/// <c>CargoPhotoUploaded</c>), а руками её перезапускает тот, кто разбирается с грузом.
/// Клиенту здесь делать нечего: вердикт модели он видит не сам по себе, а через акт приёмки
/// в cargo-service.
/// </para>
/// </summary>
[Route("api/inspections")]
[ApiController]
[Authorize(Roles = "WarehouseOperator,Manager,Admin")]
public class InspectionsController(IInspectionJobsService jobsService, IMapper mapper) : ControllerBase
{
    /// <summary>
    /// Ставит проверку вручную. Отдельного «перезапуска задания» нет: повторная проверка — это
    /// новое задание по тем же снимкам, а старое остаётся в истории со своим вердиктом или
    /// причиной отказа (уникальности по грузу в схеме нет именно ради этого).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<InspectionJobResponse>> CreateInspection(
        [FromBody] CreateInspectionRequest request,
        CancellationToken cancellationToken)
    {
        var job = await jobsService.EnqueueAsync(request.ShipmentId, request.PhotoFileIds, cancellationToken);

        // Пустой список отсекает валидатор, так что null здесь означал бы, что все переданные
        // идентификаторы совпали между собой — но и тогда задание создаётся, просто с одним
        // снимком. Ветка оставлена ради явности контракта сервиса.
        if (job is null)
            return BadRequest();

        var response = mapper.Map<InspectionJobResponse>(job);

        return CreatedAtAction(nameof(GetInspection), new { id = job.Id }, response);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<InspectionJobResponse>> GetInspection(Guid id, CancellationToken cancellationToken)
    {
        var job = await jobsService.GetByIdAsync(id, cancellationToken);
        if (job is null)
            return NotFound();

        return Ok(mapper.Map<InspectionJobResponse>(job));
    }
}
