namespace PaymentService.Application.Models;

/// <summary>
/// Настройки расчётов, нужные сценариям Application. Тип объявлен здесь (им пользуется
/// <c>InvoicesService</c>), а заполняется в Infrastructure: про <c>IConfiguration</c> знает
/// только она — тот же приём, что с <c>NotificationOptions</c> в notification-service.
/// <para>
/// Валюта берётся из той же настройки, что и у клиента провайдера (<c>PaymentProvider:Currency</c>),
/// а не из собственного ключа: два ключа рано или поздно разъедутся, и сервис выставил бы счёт в
/// одной валюте, а платёж завёл в другой.
/// </para>
/// </summary>
public sealed class PaymentOptions
{
    public required string Currency { get; init; }

    /// <summary>
    /// Перепроверять ли статус платежа у провайдера, вместо того чтобы верить телу webhook.
    /// <para>
    /// Не отдельный ключ конфигурации, а следствие того, подключён ли провайдер: с настоящим
    /// эквайрингом перепроверка обязательна (webhook приходит неаутентифицированным запросом, и
    /// поверить телу на слово значило бы позволить любому желающему объявить чужую заявку
    /// оплаченной), а на стенде с песочницей перепроверять не у кого — там webhook и есть
    /// единственный способ довести платёж до оплаты.
    /// </para>
    /// </summary>
    public required bool VerifyWebhookWithProvider { get; init; }
}
