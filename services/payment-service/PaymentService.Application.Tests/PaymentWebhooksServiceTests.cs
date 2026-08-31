using CargoService.Contracts.Events.V1;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PaymentService.Application.Interfaces;
using PaymentService.Application.Models;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;

namespace PaymentService.Application.Tests;

/// <summary>
/// Применение уведомлений провайдера. Отдельно проверяется то, ради чего сделана перепроверка:
/// одним лишь телом webhook чужую заявку оплаченной не объявить.
/// </summary>
public class PaymentWebhooksServiceTests
{
    private const string ProviderPaymentId = "provider-1";

    private readonly Mock<IInvoicesRepository> invoices = new();
    private readonly Mock<IPaymentProviderClient> provider = new();
    private readonly Mock<IRefundsService> refunds = new();
    private readonly Mock<IOutboxWriter> outbox = new();

    [Fact]
    public async Task Handle_UnknownPayment_IsIgnored()
    {
        var service = CreateService(verifyWithProvider: false);

        var outcome = await service.HandleAsync(Notification(ProviderOperationStatus.Succeeded), default);

        // Повтор ничего не изменит — провайдеру отвечаем успехом, чтобы он перестал слать.
        Assert.Equal(WebhookOutcome.Ignored, outcome);
        outbox.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_PaymentAlreadyFinal_IsIgnored()
    {
        SeedInvoice(PaymentStatus.Succeeded, InvoiceStatus.Paid);
        var service = CreateService(verifyWithProvider: false);

        var outcome = await service.HandleAsync(Notification(ProviderOperationStatus.Succeeded), default);

        Assert.Equal(WebhookOutcome.Ignored, outcome);
        outbox.VerifyNoOtherCalls();
        invoices.Verify(
            repository => repository.MarkPaymentSucceededAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_Succeeded_MarksPaidAndEnqueuesPaymentCompleted()
    {
        var (invoice, payment) = SeedInvoice(PaymentStatus.Pending, InvoiceStatus.Issued);
        var service = CreateService(verifyWithProvider: false);

        var outcome = await service.HandleAsync(Notification(ProviderOperationStatus.Succeeded), default);

        Assert.Equal(WebhookOutcome.Applied, outcome);
        invoices.Verify(
            repository => repository.MarkPaymentSucceededAsync(payment.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyEnqueued("payment-service.payment-completed", invoice.OrderId);
    }

    [Fact]
    public async Task Handle_Canceled_MarksFailedAndEnqueuesPaymentFailed()
    {
        var (invoice, payment) = SeedInvoice(PaymentStatus.Pending, InvoiceStatus.Issued);
        var service = CreateService(verifyWithProvider: false);

        var outcome = await service.HandleAsync(
            Notification(ProviderOperationStatus.Canceled, "insufficient_funds"),
            default);

        Assert.Equal(WebhookOutcome.Applied, outcome);
        invoices.Verify(
            repository => repository.MarkPaymentFailedAsync(
                payment.Id,
                It.Is<string>(reason => reason.Contains("insufficient_funds")),
                It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyEnqueued("payment-service.payment-failed", invoice.OrderId);

        // Счёт остаётся выставленным: неудачная попытка не отменяет обязательство.
        invoices.Verify(
            repository => repository.MarkPaymentSucceededAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_StillPending_ChangesNothing()
    {
        SeedInvoice(PaymentStatus.Pending, InvoiceStatus.Issued);
        var service = CreateService(verifyWithProvider: false);

        var outcome = await service.HandleAsync(Notification(ProviderOperationStatus.Pending), default);

        Assert.Equal(WebhookOutcome.Ignored, outcome);
        outbox.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ProviderContradictsWebhook_DoesNotMarkPaid()
    {
        SeedInvoice(PaymentStatus.Pending, InvoiceStatus.Issued);
        ProviderReports(ProviderOperationStatus.Pending);
        var service = CreateService(verifyWithProvider: true);

        var outcome = await service.HandleAsync(Notification(ProviderOperationStatus.Succeeded), default);

        // Ради этого перепроверка и сделана: тело webhook — неаутентифицированный запрос снаружи.
        Assert.Equal(WebhookOutcome.Ignored, outcome);
        invoices.Verify(
            repository => repository.MarkPaymentSucceededAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        outbox.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ProviderConfirms_MarksPaid()
    {
        var (_, payment) = SeedInvoice(PaymentStatus.Pending, InvoiceStatus.Issued);
        ProviderReports(ProviderOperationStatus.Succeeded);
        var service = CreateService(verifyWithProvider: true);

        var outcome = await service.HandleAsync(Notification(ProviderOperationStatus.Succeeded), default);

        Assert.Equal(WebhookOutcome.Applied, outcome);
        invoices.Verify(
            repository => repository.MarkPaymentSucceededAsync(payment.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ProviderUnavailable_AsksForRetry()
    {
        SeedInvoice(PaymentStatus.Pending, InvoiceStatus.Issued);
        provider.Setup(client => client.GetPaymentAsync(ProviderPaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderPaymentResult.Failure("connection refused"));
        var service = CreateService(verifyWithProvider: true);

        var outcome = await service.HandleAsync(Notification(ProviderOperationStatus.Succeeded), default);

        // 503 наружу: сказать, что случилось с деньгами, сейчас нечем — пусть пришлют снова.
        Assert.Equal(WebhookOutcome.TemporaryFailure, outcome);
        outbox.VerifyNoOtherCalls();
        invoices.Verify(
            repository => repository.MarkPaymentSucceededAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SucceededAfterCancellation_RefundsWithoutPublishingPaymentCompleted()
    {
        var (invoice, payment) = SeedInvoice(PaymentStatus.Pending, InvoiceStatus.Cancelled);
        var service = CreateService(verifyWithProvider: false);

        var outcome = await service.HandleAsync(Notification(ProviderOperationStatus.Succeeded), default);

        Assert.Equal(WebhookOutcome.Applied, outcome);

        // Деньги записаны полученными и тут же отправлены на возврат...
        invoices.Verify(
            repository => repository.MarkPaymentSucceededAsync(payment.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        refunds.Verify(
            service => service.RefundPaymentAsync(invoice, payment, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // ...а PaymentCompleted не публикуется: заявка отменена, и оживлять её нельзя.
        outbox.VerifyNoOtherCalls();
    }

    private PaymentWebhooksService CreateService(bool verifyWithProvider) =>
        new(
            invoices.Object,
            provider.Object,
            refunds.Object,
            outbox.Object,
            new PaymentOptions { Currency = "RUB", VerifyWebhookWithProvider = verifyWithProvider },
            NullLogger<PaymentWebhooksService>.Instance);

    private (Invoice Invoice, Payment Payment) SeedInvoice(PaymentStatus paymentStatus, InvoiceStatus invoiceStatus)
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            Amount = 1250.50m,
            Status = paymentStatus,
            ProviderPaymentId = ProviderPaymentId,
        };

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            OrderNumber = "20260830-ABC123",
            Amount = 1250.50m,
            Currency = "RUB",
            Status = invoiceStatus,
            Payments = [payment],
        };

        payment.InvoiceId = invoice.Id;

        invoices.Setup(repository => repository.GetByProviderPaymentIdAsync(ProviderPaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        return (invoice, payment);
    }

    private void ProviderReports(ProviderOperationStatus status) =>
        provider.Setup(client => client.GetPaymentAsync(ProviderPaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderPaymentResult.Success(ProviderPaymentId, status, 1250.50m));

    private static PaymentWebhookNotification Notification(
        ProviderOperationStatus status,
        string? cancellationReason = null) =>
        new()
        {
            ProviderPaymentId = ProviderPaymentId,
            ClaimedStatus = status,
            CancellationReason = cancellationReason,
        };

    private void VerifyEnqueued(string routingKey, Guid orderId) =>
        outbox.Verify(
            writer => writer.Enqueue(
                It.IsAny<Guid>(),
                routingKey,
                It.Is<string>(payload => payload.Contains(orderId.ToString())),
                It.IsAny<DateTimeOffset>()),
            Times.Once);
}
