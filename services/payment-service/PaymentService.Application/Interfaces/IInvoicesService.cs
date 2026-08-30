namespace PaymentService.Application.Interfaces;

public interface IInvoicesService
{
    /// <summary>
    /// Выставляет счёт по подтверждённой заявке и заводит платёж у провайдера, получая платёжную
    /// ссылку для клиента.
    /// </summary>
    Task IssueForConfirmedOrderAsync(
        Guid orderId,
        string orderNumber,
        decimal amount,
        CancellationToken cancellationToken);
}
