namespace DocumentService.Application.Models;

/// <summary>
/// Обстоятельства приёмки груза — то, что сервису сообщило событие <c>CargoAccepted</c>.
/// Отдельный тип, а не сам контракт события: сценарий «какие документы полагаются» не должен
/// зависеть от формы чужого сообщения (и от того, каким событием его однажды заменят).
/// </summary>
public sealed record CargoAcceptanceFacts
{
    public required Guid ShipmentId { get; init; }
    public required Guid OrderId { get; init; }
    public required string TrackingNumber { get; init; }
    public required DateTimeOffset AcceptedAt { get; init; }

    /// <summary>Состояния как их зафиксировал сотрудник — строками: чужих enum'ов сервис не знает.</summary>
    public required string PackagingCondition { get; init; }
    public required string CargoCondition { get; init; }
}

/// <summary>Обстоятельства выдачи груза — из события <c>CargoDelivered</c>.</summary>
public sealed record CargoDeliveryFacts
{
    public required Guid ShipmentId { get; init; }
    public required Guid OrderId { get; init; }
    public required string TrackingNumber { get; init; }
    public required DateTimeOffset DeliveredAt { get; init; }

    /// <summary>Кто принял груз. Событие поле не заполняет (Фаза 5), поэтому необязательное.</summary>
    public string? ReceivedByName { get; init; }
}
