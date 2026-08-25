namespace CargoService.Domain.Enums;

/// <summary>
/// Вид упаковки. Значения намеренно совпадают с <c>OrdersService.Domain.Enums.PackagingType</c>:
/// в заявке клиент указывает желаемую упаковку, здесь фиксируется фактически выполненная.
/// Общего типа нет сознательно — «база на сервис» означает и независимые модели
/// (CargoService.Contracts существует только для событий RabbitMQ).
/// </summary>
public enum PackagingType
{
    Wooden,
    Pallet,
    Special
}
