using PaymentService.Application.Models;

namespace PaymentService.Application.Interfaces;

/// <summary>
/// Внешний платёжный провайдер (эквайринг). Реализации живут в Infrastructure — боевая под
/// контракт конкретного провайдера и песочница для локального стенда.
/// <para>
/// Границей проходит ровно то, что сервису нужно от денег снаружи: создать платёж и получить
/// ссылку на оплату, спросить статус, вернуть деньги. Карточных данных здесь нет и быть не
/// должно — плательщик вводит их на стороне провайдера, куда его уводит
/// <see cref="ProviderPaymentResult.ConfirmationUrl"/>; сервис хранит только идентификатор
/// транзакции. Это же держит его вне области PCI DSS.
/// </para>
/// </summary>
public interface IPaymentProviderClient
{
    /// <summary>
    /// Заводит платёж у провайдера и возвращает платёжную ссылку, по которой платит клиент.
    /// </summary>
    Task<ProviderPaymentResult> CreatePaymentAsync(ProviderPaymentRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Спрашивает актуальный статус платежа. Нужен как подстраховка к webhook: уведомление
    /// провайдера может не дойти, и тогда истина берётся отсюда.
    /// </summary>
    Task<ProviderPaymentResult> GetPaymentAsync(string providerPaymentId, CancellationToken cancellationToken);

    /// <summary>Возврат по ранее проведённому платежу (полный или частичный).</summary>
    Task<ProviderRefundResult> RefundAsync(ProviderRefundRequest request, CancellationToken cancellationToken);
}
