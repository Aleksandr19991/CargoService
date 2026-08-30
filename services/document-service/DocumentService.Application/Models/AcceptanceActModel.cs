namespace DocumentService.Application.Models;

/// <summary>Какую передачу груза оформляет акт.</summary>
public enum HandoverStage
{
    /// <summary>Приёмка складом: груз сдал отправитель, принял перевозчик.</summary>
    Acceptance,

    /// <summary>Выдача: груз сдал перевозчик, принял адресат.</summary>
    Delivery
}

/// <summary>
/// Данные акта приёма-передачи. Один вид документа на обе передачи груза — на приёмке и на
/// выдаче: различаются они данными и подписантами, а не бланком, поэтому вместо второго типа
/// документа заведена <see cref="Stage"/>.
/// </summary>
public sealed record AcceptanceActModel
{
    public required string TrackingNumber { get; init; }
    public required DateTimeOffset HandedOverAt { get; init; }
    public required HandoverStage Stage { get; init; }

    public string? OrderNumber { get; init; }

    /// <summary>Состояния из приёмки — строками: чужих enum'ов сервис не знает. У выдачи их нет.</summary>
    public string? PackagingCondition { get; init; }
    public string? CargoCondition { get; init; }

    /// <summary>Кто принял груз при выдаче (из события выдачи).</summary>
    public string? ReceivedByName { get; init; }

    public string? SenderName { get; init; }
    public string? RecipientName { get; init; }
    public string? CargoName { get; init; }
}
