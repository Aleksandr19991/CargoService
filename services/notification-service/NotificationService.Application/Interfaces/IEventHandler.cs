namespace NotificationService.Application.Interfaces;

/// <summary>
/// Обработчик одного интеграционного события. Реализации живут в Application (по классу на
/// событие), а вся возня с RabbitMQ — в единственном обобщённом консьюмере Infrastructure:
/// событий сервис слушает десяток, и десять копий одной и той же топологии очередей были бы
/// десятью местами, где эту топологию можно разойтись.
/// </summary>
public interface IEventHandler<in TEvent>
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
}
