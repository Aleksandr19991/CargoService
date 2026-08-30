using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Interfaces;
using PaymentService.Application.Models;

namespace PaymentService.Infrastructure.Payments;

/// <summary>
/// Заглушка эквайринга на время, пока провайдер не подключён: заводит платёж у себя в памяти и
/// отдаёт «платёжную ссылку», по которой ничего не списывается. Регистрируется вместо
/// <see cref="YooKassaPaymentProviderClient"/>, когда в конфигурации нет учётных данных магазина.
/// <para>
/// В отличие от <c>LoggingSmsSender</c> в notification-service, который отчитывается об успехе,
/// созданный здесь платёж остаётся <see cref="ProviderOperationStatus.Pending"/> и сам никогда
/// не становится оплаченным. Это сознательно: «отправленное» SMS ничего не стоит, а платёж,
/// который на dev-стенде сам себя проводит, приучал бы к тому, что деньги приходят без
/// подтверждения — и первая же настоящая интеграция сломала бы все построенные на этом сценарии.
/// Довести платёж до оплаты на стенде можно так же, как это сделал бы провайдер, — постучав в
/// webhook сервиса (Фаза 9, задача 4).
/// </para>
/// <para>
/// Хранилище — словарь в памяти процесса: он нужен, чтобы <see cref="GetPaymentAsync"/> отвечал
/// согласованно с тем, что вернул <see cref="CreatePaymentAsync"/>, и чтобы повтор с тем же
/// ключом идемпотентности отдавал тот же платёж, а не новый (иначе на заглушке нельзя было бы
/// проверить защиту от повторной обработки события). Рестарт сервиса его теряет — стенду
/// достаточно, для чего-то большего нужен настоящий провайдер.
/// </para>
/// </summary>
public class SandboxPaymentProviderClient(
    PaymentProviderOptions options,
    ILogger<SandboxPaymentProviderClient> logger) : IPaymentProviderClient
{
    private readonly ConcurrentDictionary<Guid, ProviderPaymentResult> paymentsByIdempotenceKey = new();
    private readonly ConcurrentDictionary<string, ProviderPaymentResult> paymentsById = new();

    public Task<ProviderPaymentResult> CreatePaymentAsync(
        ProviderPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var payment = paymentsByIdempotenceKey.GetOrAdd(request.IdempotenceKey, _ =>
        {
            var providerPaymentId = $"sandbox-{Guid.NewGuid():N}";

            return ProviderPaymentResult.Success(
                providerPaymentId,
                ProviderOperationStatus.Pending,
                request.Amount,
                confirmationUrl: $"{options.ReturnUrl}?sandboxPaymentId={providerPaymentId}");
        });

        paymentsById[payment.ProviderPaymentId!] = payment;

        logger.LogWarning(
            "Payment provider is not configured; sandbox payment {PaymentId} for order {OrderId} on {Amount} {Currency} stays pending",
            payment.ProviderPaymentId,
            request.OrderId,
            request.Amount,
            request.Currency);

        return Task.FromResult(payment);
    }

    public Task<ProviderPaymentResult> GetPaymentAsync(string providerPaymentId, CancellationToken cancellationToken)
    {
        // Настоящий провайдер на незнакомый идентификатор отвечает 404 — заглушка ведёт себя так же,
        // чтобы вызывающий код не писался под её снисходительность.
        var result = paymentsById.TryGetValue(providerPaymentId, out var payment)
            ? payment
            : ProviderPaymentResult.Failure($"Sandbox payment provider does not know payment {providerPaymentId}.");

        return Task.FromResult(result);
    }

    public Task<ProviderRefundResult> RefundAsync(ProviderRefundRequest request, CancellationToken cancellationToken)
    {
        // Возврат заглушка подтверждает всегда, даже по неизвестному ей платежу: словарь живёт до
        // ближайшего рестарта, а сценарий отмены заявки должен на стенде доходить до конца.
        // Признак оплаты у себя сервис хранит в БД и проверяет его сам — до провайдера дело
        // доходит уже после этой проверки.
        logger.LogWarning(
            "Payment provider is not configured; sandbox refund of {Amount} {Currency} for payment {PaymentId} was not actually made",
            request.Amount,
            request.Currency,
            request.ProviderPaymentId);

        return Task.FromResult(ProviderRefundResult.Success(
            $"sandbox-refund-{Guid.NewGuid():N}",
            ProviderOperationStatus.Succeeded,
            request.Amount));
    }
}
