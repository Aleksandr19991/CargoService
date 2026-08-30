using Microsoft.Extensions.Logging;
using PaymentService.Application.Interfaces;
using PaymentService.Application.Models;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;

namespace PaymentService.Application;

/// <summary>
/// Выставление счетов по подтверждённым заявкам.
/// </summary>
public class InvoicesService(
    IInvoicesRepository invoices,
    IPaymentProviderClient paymentProvider,
    PaymentOptions options,
    ILogger<InvoicesService> logger) : IInvoicesService
{
    public async Task IssueForConfirmedOrderAsync(
        Guid orderId,
        string orderNumber,
        decimal amount,
        CancellationToken cancellationToken)
    {
        if (amount <= 0)
        {
            // Подтверждённая заявка без стоимости — ошибка вышестоящего расчёта, а не бизнес-случай.
            // Счёт не выставляется: пустой счёт нечего оплачивать, а помечать его оплаченным
            // значило бы сказать orders-service, что деньги получены, чего не было.
            logger.LogError(
                "Order {OrderNumber} ({OrderId}) was confirmed with a non-positive price {Amount}; no invoice issued",
                orderNumber, orderId, amount);
            return;
        }

        // Вторая линия защиты от дублей рядом с inbox: отметка об обработанном событии ставится
        // ПОСЛЕ обработки, и падение между ними приведёт к повторной доставке — счёт по заявке
        // должен быть один независимо от того, сколько раз пришло событие.
        var existing = await invoices.GetByOrderIdAsync(orderId, cancellationToken);
        if (existing is not null)
        {
            logger.LogInformation(
                "Invoice {InvoiceId} for order {OrderNumber} already exists, skipping",
                existing.Id, orderNumber);
            return;
        }

        var now = DateTimeOffset.UtcNow;

        // Идентификаторы задаются здесь, а не оставляются на генерацию при сохранении: Id платежа
        // уходит провайдеру ключом идемпотентности, и он должен быть известен до записи в БД.
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            Amount = amount,
            Status = PaymentStatus.Pending,
            CreatedAt = now,
        };

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            OrderNumber = orderNumber,
            Amount = amount,
            Currency = options.Currency,
            Status = InvoiceStatus.Issued,
            CreatedAt = now,
            Payments = [payment],
        };

        // Счёт и платёж сохраняются ДО обращения к провайдеру: обратный порядок оставлял бы у
        // провайдера заведённый платёж, о котором сервис ничего не знает, упади он между вызовом
        // и записью (та же беда, что с пользователем-сиротой в Keycloak у identity-service).
        await invoices.AddAsync(invoice, cancellationToken);

        var result = await paymentProvider.CreatePaymentAsync(
            new ProviderPaymentRequest
            {
                IdempotenceKey = payment.Id,
                OrderId = orderId,
                Amount = amount,
                Currency = invoice.Currency,
                Description = $"Оплата заявки {orderNumber}",
            },
            cancellationToken);

        if (!result.IsSuccess)
        {
            logger.LogError(
                "Failed to create a provider payment for order {OrderNumber}: {Reason}",
                orderNumber, result.FailureReason);

            await invoices.MarkPaymentFailedAsync(payment.Id, result.FailureReason!, cancellationToken);
            return;
        }

        // Статус платежа остаётся Pending, даже если провайдер уже ответил чем-то другим: при
        // redirect-схеме свежесозданный платёж всегда ждёт оплаты, а окончательный исход приходит
        // webhook'ом (задача 4 Фазы 9) — там же и живёт перевод счёта в Paid.
        await invoices.SetProviderPaymentAsync(
            payment.Id,
            result.ProviderPaymentId!,
            result.ConfirmationUrl,
            cancellationToken);

        logger.LogInformation(
            "Issued invoice {InvoiceId} for order {OrderNumber} on {Amount} {Currency}, provider payment {ProviderPaymentId}",
            invoice.Id, orderNumber, amount, invoice.Currency, result.ProviderPaymentId);
    }
}
