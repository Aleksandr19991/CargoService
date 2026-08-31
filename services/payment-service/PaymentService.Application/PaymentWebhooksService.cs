using System.Text.Json;
using CargoService.Contracts.Events.V1;
using CargoService.Contracts.Messaging;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Interfaces;
using PaymentService.Application.Models;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;

namespace PaymentService.Application;

/// <summary>
/// Применение уведомлений платёжного провайдера: перевод платежа в окончательный статус и
/// публикация исхода остальным сервисам.
/// </summary>
public class PaymentWebhooksService(
    IInvoicesRepository invoices,
    IPaymentProviderClient paymentProvider,
    IRefundsService refunds,
    IOutboxWriter outboxWriter,
    PaymentOptions options,
    ILogger<PaymentWebhooksService> logger) : IPaymentWebhooksService
{
    private const string PublishingService = "payment-service";

    public async Task<WebhookOutcome> HandleAsync(
        PaymentWebhookNotification notification,
        CancellationToken cancellationToken)
    {
        var invoice = await invoices.GetByProviderPaymentIdAsync(notification.ProviderPaymentId, cancellationToken);
        if (invoice is null)
        {
            // Чужой или уже забытый платёж. Повтор ничего не изменит, поэтому уведомление
            // принимается — но с предупреждением: это либо чужой webhook, либо потерянная строка.
            logger.LogWarning(
                "Webhook for unknown provider payment {ProviderPaymentId}",
                notification.ProviderPaymentId);
            return WebhookOutcome.Ignored;
        }

        var payment = invoice.Payments.First(stored => stored.ProviderPaymentId == notification.ProviderPaymentId);
        if (payment.Status != PaymentStatus.Pending)
        {
            // Провайдеры доставляют уведомления at-least-once, и повторная доставка исхода —
            // обычное дело, а не ошибка: второй раз публиковать `PaymentCompleted` нельзя.
            logger.LogInformation(
                "Payment {PaymentId} is already {Status}, webhook ignored",
                payment.Id, payment.Status);
            return WebhookOutcome.Ignored;
        }

        var (status, cancellationReason, verificationFailed) =
            await ResolveStatusAsync(notification, cancellationToken);

        if (verificationFailed)
            return WebhookOutcome.TemporaryFailure;

        return status switch
        {
            ProviderOperationStatus.Succeeded => await ApplySuccessAsync(invoice, payment, cancellationToken),
            ProviderOperationStatus.Canceled => await ApplyFailureAsync(invoice, payment, cancellationReason, cancellationToken),
            _ => Ignore(payment),
        };
    }

    /// <summary>
    /// Тело webhook — это неаутентифицированный запрос снаружи, и с подключённым провайдером
    /// статус берётся не из него, а из ответа самого провайдера на прямой запрос: иначе объявить
    /// чужую заявку оплаченной мог бы кто угодно, знающий идентификатор платежа. На стенде с
    /// песочницей перепроверять не у кого — там уведомление и есть единственный способ довести
    /// платёж до оплаты.
    /// </summary>
    private async Task<(ProviderOperationStatus Status, string? CancellationReason, bool VerificationFailed)>
        ResolveStatusAsync(PaymentWebhookNotification notification, CancellationToken cancellationToken)
    {
        if (!options.VerifyWebhookWithProvider)
        {
            logger.LogWarning(
                "Payment provider is not configured; webhook payload for {ProviderPaymentId} is taken at face value",
                notification.ProviderPaymentId);
            return (notification.ClaimedStatus, notification.CancellationReason, false);
        }

        var verified = await paymentProvider.GetPaymentAsync(notification.ProviderPaymentId, cancellationToken);
        if (!verified.IsSuccess)
        {
            // Провайдер недоступен — сказать, что случилось с деньгами, сейчас нечем. Ничего не
            // меняем и просим прислать уведомление снова.
            logger.LogError(
                "Failed to verify webhook for payment {ProviderPaymentId} with the provider: {Reason}",
                notification.ProviderPaymentId, verified.FailureReason);
            return (ProviderOperationStatus.Pending, null, true);
        }

        if (verified.Status != notification.ClaimedStatus)
        {
            logger.LogWarning(
                "Webhook for payment {ProviderPaymentId} claimed {ClaimedStatus} but the provider reports {ActualStatus}",
                notification.ProviderPaymentId, notification.ClaimedStatus, verified.Status);
        }

        return (verified.Status, verified.CancellationReason, false);
    }

    private async Task<WebhookOutcome> ApplySuccessAsync(
        Invoice invoice,
        Payment payment,
        CancellationToken cancellationToken)
    {
        if (invoice.Status == InvoiceStatus.Cancelled)
            return await RefundLatePaymentAsync(invoice, payment, cancellationToken);

        var completed = new PaymentCompleted
        {
            OrderId = invoice.OrderId,
            PaymentId = payment.Id,
            Amount = payment.Amount,
        };

        // Событие ставится в outbox ДО сохранения: оба идут через один DbContext, поэтому
        // «платёж оплачен» и «об этом сказано остальным» коммитятся одной транзакцией.
        outboxWriter.Enqueue(
            completed.EventId,
            RabbitMqConventions.RoutingKey(PublishingService, nameof(PaymentCompleted)),
            JsonSerializer.Serialize(completed),
            completed.OccurredAtUtc);

        await invoices.MarkPaymentSucceededAsync(payment.Id, cancellationToken);

        logger.LogInformation(
            "Payment {PaymentId} for order {OrderNumber} succeeded, invoice {InvoiceId} is paid",
            payment.Id, invoice.OrderNumber, invoice.Id);

        return WebhookOutcome.Applied;
    }

    /// <summary>
    /// Оплата пришла уже после отмены заявки: платёжная ссылка живёт на стороне провайдера, и
    /// отмена заявки клиента по ней заплатить не мешает. Деньги записываются полученными и тут
    /// же уезжают на возврат.
    /// <para>
    /// <c>PaymentCompleted</c> при этом не публикуется: заявка отменена, и сказать о ней
    /// «оплачена» значило бы попросить orders-service оживить отменённый заказ. Наружу уходит
    /// только <c>RefundIssued</c> — то, что с деньгами в итоге и произошло.
    /// </para>
    /// </summary>
    private async Task<WebhookOutcome> RefundLatePaymentAsync(
        Invoice invoice,
        Payment payment,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Payment {PaymentId} succeeded after order {OrderNumber} was cancelled; refunding",
            payment.Id, invoice.OrderNumber);

        await invoices.MarkPaymentSucceededAsync(payment.Id, cancellationToken);

        await refunds.RefundPaymentAsync(
            invoice,
            payment,
            $"Оплата поступила после отмены заявки {invoice.OrderNumber}",
            cancellationToken);

        return WebhookOutcome.Applied;
    }

    private async Task<WebhookOutcome> ApplyFailureAsync(
        Invoice invoice,
        Payment payment,
        string? cancellationReason,
        CancellationToken cancellationToken)
    {
        var reason = string.IsNullOrWhiteSpace(cancellationReason)
            ? "Платёж отменён провайдером"
            : $"Платёж отменён провайдером: {cancellationReason}";

        var failed = new PaymentFailed
        {
            OrderId = invoice.OrderId,
            PaymentId = payment.Id,
            Reason = reason,
        };

        outboxWriter.Enqueue(
            failed.EventId,
            RabbitMqConventions.RoutingKey(PublishingService, nameof(PaymentFailed)),
            JsonSerializer.Serialize(failed),
            failed.OccurredAtUtc);

        // Счёт остаётся `Issued`: неудачная попытка не отменяет обязательство — клиент вправе
        // попробовать оплатить снова.
        await invoices.MarkPaymentFailedAsync(payment.Id, reason, cancellationToken);

        logger.LogInformation(
            "Payment {PaymentId} for order {OrderNumber} failed: {Reason}",
            payment.Id, invoice.OrderNumber, reason);

        return WebhookOutcome.Applied;
    }

    private WebhookOutcome Ignore(Payment payment)
    {
        // Промежуточные уведомления провайдера (платёж создан, ждёт подтверждения) исхода не
        // несут — записывать нечего.
        logger.LogInformation("Payment {PaymentId} is still pending, webhook ignored", payment.Id);
        return WebhookOutcome.Ignored;
    }
}
