namespace PaymentService.Application.Interfaces;

/// <summary>
/// Обработчик одного интеграционного события. Реализации живут в Application (по классу на
/// событие), а вся возня с RabbitMQ — в единственном обобщённом консьюмере Infrastructure.
/// </summary>
public interface IEventHandler<in TEvent>
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
}
