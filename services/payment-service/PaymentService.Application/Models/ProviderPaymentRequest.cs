namespace PaymentService.Application.Models;

/// <summary>Что сервис просит завести у провайдера при выставлении счёта.</summary>
public sealed record ProviderPaymentRequest
{
    /// <summary>
    /// Ключ идемпотентности. Провайдеры эквайринга требуют его на создающих запросах, и он же
    /// делает безопасными повторы: HTTP-клиент ходит с retry-политикой, а консьюмер
    /// <c>OrderConfirmed</c> может получить событие второй раз — без ключа каждая такая попытка
    /// заводила бы новый платёж и второй раз списывала с клиента деньги. Значение берётся из
    /// идентификатора нашего счёта, а не генерируется на каждый вызов.
    /// </summary>
    public required Guid IdempotenceKey { get; init; }

    public required Guid OrderId { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    /// <summary>Назначение платежа — его видит плательщик на странице оплаты.</summary>
    public required string Description { get; init; }

    /// <summary>
    /// Куда провайдер вернёт плательщика после оплаты. Null — взять адрес из настроек сервиса
    /// (обычный случай); поле оставлено на будущее, когда витрин станет больше одной.
    /// </summary>
    public string? ReturnUrl { get; init; }
}
