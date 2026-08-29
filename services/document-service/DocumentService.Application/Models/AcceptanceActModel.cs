namespace DocumentService.Application.Models;

/// <summary>
/// Данные акта приёма-передачи: что склад принял и в каком виде. Составляется по событию
/// <c>CargoAccepted</c> (задача 4), поэтому набор полей повторяет то, что в нём есть.
/// </summary>
public sealed record AcceptanceActModel
{
    public required string TrackingNumber { get; init; }
    public required DateTimeOffset AcceptedAt { get; init; }

    /// <summary>Состояния как их зафиксировал сотрудник — строками: чужих enum'ов сервис не знает.</summary>
    public required string PackagingCondition { get; init; }
    public required string CargoCondition { get; init; }

    public string? OrderNumber { get; init; }

    /// <summary>
    /// Кто принял груз. В событии приезжает идентификатор сотрудника, а не имя: подставлять в
    /// печатный акт GUID бессмысленно, поэтому поле необязательное — имя появится, если сервис
    /// научится его получать (см. задачу 4).
    /// </summary>
    public string? InspectedByName { get; init; }

    /// <summary>Сколько снимков приложено к приёмке — печатается как отсылка к фотофиксации.</summary>
    public int PhotoCount { get; init; }
}
