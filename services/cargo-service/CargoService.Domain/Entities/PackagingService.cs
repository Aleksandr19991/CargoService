using CargoService.Domain.Enums;

namespace CargoService.Domain.Entities;

/// <summary>
/// Фактически оказанная услуга упаковки. Несмотря на суффикс «Service» из ТЗ — это доменная
/// сущность (строка в БД), а не сервис слоя Application.
/// </summary>
public class PackagingService
{
    public Guid Id { get; set; }
    public Guid ShipmentId { get; set; }

    public PackagingType Type { get; set; }

    // References User.Id in identity-service (сотрудник, выполнивший упаковку).
    public Guid PerformedByUserId { get; set; }

    public DateTimeOffset PerformedAt { get; set; }
}
