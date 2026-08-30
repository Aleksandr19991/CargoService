namespace PaymentService.Infrastructure.Payments;

/// <summary>
/// Настройки платёжного провайдера. Реализация написана под контракт ЮKassa (redirect-эквайринг:
/// платёж создаётся запросом с сервера, клиент уходит по ссылке на страницу провайдера), но
/// <see cref="BaseUrl"/> вынесен в конфигурацию: тот же код проверяется против песочницы или
/// локального стенда, а при переезде на другого провайдера меняется одна реализация
/// <c>IPaymentProviderClient</c>, а не всё, что в сервисе связано с деньгами.
/// </summary>
public sealed class PaymentProviderOptions
{
    public const string SectionName = "PaymentProvider";

    public required string BaseUrl { get; init; }

    /// <summary>
    /// Пустые <see cref="ShopId"/>/<see cref="SecretKey"/> означают «провайдер не подключён» —
    /// тогда вместо боевого клиента регистрируется <see cref="SandboxPaymentProviderClient"/>.
    /// </summary>
    public string? ShopId { get; init; }

    public string? SecretKey { get; init; }

    /// <summary>Валюта расчётов в формате ISO 4217. Тарифы платформы считаются в рублях.</summary>
    public required string Currency { get; init; }

    /// <summary>Куда провайдер возвращает плательщика после оплаты.</summary>
    public required string ReturnUrl { get; init; }
}
