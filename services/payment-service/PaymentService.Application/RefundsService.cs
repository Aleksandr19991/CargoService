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
/// Денежная сторона отменённой заявки: закрытие неоплаченного счёта и возврат оплаченного.
/// </summary>
// Валюта возврата берётся из счёта, а не из настроек: вернуть надо ровно то, что было принято,
// даже если валюта расчётов платформы с тех пор поменялась.
public class RefundsService(
    IInvoicesRepository invoices,
    IPaymentProviderClient paymentProvider,
    IOutboxWriter outboxWriter,
    ILogger<RefundsService> logger) : IRefundsService
{
    private const string PublishingService = "payment-service";

    public async Task RefundForCancelledOrderAsync(
        Guid orderId,
        string orderNumber,
        string? reason,
        CancellationToken cancellationToken)
    {
        var invoice = await invoices.GetByOrderIdAsync(orderId, cancellationToken);
        if (invoice is null)
        {
            // Отмена до подтверждения заявки: счёт выставляется только по `OrderConfirmed`, и
            // возвращать нечего. Обычный ход событий, а не ошибка.
            logger.LogInformation("Order {OrderNumber} was cancelled with no invoice issued", orderNumber);
            return;
        }

        if (invoice.Status is InvoiceStatus.Cancelled or InvoiceStatus.Refunded)
        {
            logger.LogInformation(
                "Invoice {InvoiceId} is already {Status}, cancellation ignored",
                invoice.Id, invoice.Status);
            return;
        }

        var refundReason = BuildReason(orderNumber, reason);

        var paidPayment = invoice.Payments.FirstOrDefault(payment => payment.Status == PaymentStatus.Succeeded);
        if (paidPayment is null)
        {
            // Счёт не оплачен — возвращать нечего, но и висеть выставленным он больше не должен.
            // Незавершённые платежи намеренно не трогаются: платёжная ссылка живёт на стороне
            // провайдера, и клиент всё ещё может по ней заплатить. Такой платёж придёт webhook'ом
            // и уедет на возврат — счёт к тому времени уже `Cancelled`, и это тот признак, по
            // которому его отличают от обычной оплаты.
            await invoices.MarkInvoiceCancelledAsync(invoice.Id, cancellationToken);

            logger.LogInformation(
                "Invoice {InvoiceId} for cancelled order {OrderNumber} closed unpaid",
                invoice.Id, orderNumber);
            return;
        }

        await RefundPaymentAsync(invoice, paidPayment, refundReason, cancellationToken);
    }

    public async Task RefundPaymentAsync(
        Invoice invoice,
        Payment payment,
        string reason,
        CancellationToken cancellationToken)
    {
        var refund = new Refund
        {
            // Id задаётся здесь: он уходит провайдеру ключом идемпотентности, и повтор с ним
            // вернёт клиенту деньги один раз, а не столько, сколько было попыток.
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            PaymentId = payment.Id,
            Amount = payment.Amount,
            Status = RefundStatus.Pending,
            Reason = reason,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        // Запись заводится ДО обращения к провайдеру — по той же причине, что и у платежа, но
        // здесь цена ошибки выше: возврат, сделанный у провайдера и не записанный у себя, никак
        // не отличить от несделанного, и второй раз вернуть деньги ничто не помешает.
        await invoices.AddRefundAsync(refund, cancellationToken);

        var result = await paymentProvider.RefundAsync(
            new ProviderRefundRequest
            {
                IdempotenceKey = refund.Id,
                ProviderPaymentId = payment.ProviderPaymentId!,
                Amount = refund.Amount,
                Currency = invoice.Currency,
                Description = reason,
            },
            cancellationToken);

        if (!result.IsSuccess)
        {
            logger.LogError(
                "Refund {RefundId} for order {OrderNumber} was rejected by the provider: {Reason}",
                refund.Id, invoice.OrderNumber, result.FailureReason);

            // Счёт остаётся оплаченным: деньги всё ещё у платформы, и объявлять их возвращёнными
            // нельзя. Разбираться с такими возвратами придётся вручную — автоматических повторов
            // здесь пока нет.
            await invoices.MarkRefundFailedAsync(refund.Id, result.FailureReason!, cancellationToken);
            return;
        }

        if (result.Status != ProviderOperationStatus.Succeeded)
        {
            // Провайдер принял возврат, но деньги ещё не дошли. Событие не публикуется: сказать
            // клиенту «деньги вернулись», когда они в пути, значит соврать. Отдельного webhook
            // на завершение возврата сервис пока не слушает — такие возвраты видны в БД как
            // `Pending`.
            logger.LogWarning(
                "Refund {RefundId} for order {OrderNumber} is {Status} at the provider",
                refund.Id, invoice.OrderNumber, result.Status);
            return;
        }

        var issued = new RefundIssued
        {
            OrderId = invoice.OrderId,
            PaymentId = payment.Id,
            RefundId = refund.Id,
            Amount = refund.Amount,
            Reason = reason,
        };

        outboxWriter.Enqueue(
            issued.EventId,
            RabbitMqConventions.RoutingKey(PublishingService, nameof(RefundIssued)),
            JsonSerializer.Serialize(issued),
            issued.OccurredAtUtc);

        await invoices.MarkRefundSucceededAsync(refund.Id, result.ProviderRefundId!, cancellationToken);

        logger.LogInformation(
            "Refunded {Amount} {Currency} for order {OrderNumber}, refund {RefundId}",
            refund.Amount, invoice.Currency, invoice.OrderNumber, refund.Id);
    }

    private static string BuildReason(string orderNumber, string? reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? $"Отмена заявки {orderNumber}"
            : $"Отмена заявки {orderNumber}: {reason}";
}
