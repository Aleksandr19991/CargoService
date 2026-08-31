using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PaymentService.Application.Interfaces;
using PaymentService.Application.Models;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;

namespace PaymentService.Application.Tests;

/// <summary>Возвраты по отменённым заявкам.</summary>
public class RefundsServiceTests
{
    private const string ProviderPaymentId = "provider-1";

    private readonly Mock<IInvoicesRepository> invoices = new();
    private readonly Mock<IPaymentProviderClient> provider = new();
    private readonly Mock<IOutboxWriter> outbox = new();
    private readonly RefundsService service;

    private readonly Guid orderId = Guid.NewGuid();

    public RefundsServiceTests()
    {
        service = new RefundsService(
            invoices.Object,
            provider.Object,
            outbox.Object,
            NullLogger<RefundsService>.Instance);
    }

    [Fact]
    public async Task Cancel_NoInvoice_DoesNothing()
    {
        await service.RefundForCancelledOrderAsync(orderId, "20260830-NOINV1", null, default);

        // Счёт выставляется только по OrderConfirmed — отмена до подтверждения это обычный ход дел.
        invoices.Verify(
            repository => repository.MarkInvoiceCancelledAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        provider.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Cancel_UnpaidInvoice_ClosesItWithoutRefund()
    {
        var invoice = SeedInvoice(InvoiceStatus.Issued, PaymentStatus.Pending);

        await service.RefundForCancelledOrderAsync(orderId, "20260830-UNPAID1", null, default);

        invoices.Verify(
            repository => repository.MarkInvoiceCancelledAsync(invoice.Id, It.IsAny<CancellationToken>()),
            Times.Once);

        // Незавершённый платёж не трогается: ссылка живёт у провайдера, и оплата по ней ещё может
        // прийти — пометить его Failed значило бы отбросить тот webhook и потерять деньги.
        invoices.Verify(
            repository => repository.MarkPaymentFailedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        invoices.Verify(
            repository => repository.AddRefundAsync(It.IsAny<Refund>(), It.IsAny<CancellationToken>()),
            Times.Never);
        provider.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(InvoiceStatus.Cancelled)]
    [InlineData(InvoiceStatus.Refunded)]
    public async Task Cancel_AlreadyClosedInvoice_IsIgnored(InvoiceStatus status)
    {
        SeedInvoice(status, PaymentStatus.Succeeded);

        await service.RefundForCancelledOrderAsync(orderId, "20260830-REFND1", null, default);

        invoices.Verify(
            repository => repository.AddRefundAsync(It.IsAny<Refund>(), It.IsAny<CancellationToken>()),
            Times.Never);
        provider.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Cancel_PaidInvoice_RefundsAndEnqueuesRefundIssued()
    {
        var invoice = SeedInvoice(InvoiceStatus.Paid, PaymentStatus.Succeeded);
        Refund? saved = CaptureRefund();
        ProviderRefunds(ProviderOperationStatus.Succeeded);

        await service.RefundForCancelledOrderAsync(orderId, "20260830-REFND1", "клиент передумал", default);

        saved = LastRefund;
        Assert.NotNull(saved);
        Assert.Equal(invoice.Amount, saved.Amount);
        Assert.Contains("клиент передумал", saved.Reason);

        outbox.Verify(
            writer => writer.Enqueue(
                It.IsAny<Guid>(),
                "payment-service.refund-issued",
                It.Is<string>(payload => payload.Contains(orderId.ToString())),
                It.IsAny<DateTimeOffset>()),
            Times.Once);

        invoices.Verify(
            repository => repository.MarkRefundSucceededAsync(saved.Id, "refund-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Cancel_PaidInvoice_UsesRefundIdAsIdempotenceKeyAndInvoiceCurrency()
    {
        SeedInvoice(InvoiceStatus.Paid, PaymentStatus.Succeeded);
        CaptureRefund();

        ProviderRefundRequest? sent = null;
        provider.Setup(client => client.RefundAsync(It.IsAny<ProviderRefundRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderRefundRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(ProviderRefundResult.Success("refund-1", ProviderOperationStatus.Succeeded, 1250.50m));

        await service.RefundForCancelledOrderAsync(orderId, "20260830-REFND1", null, default);

        Assert.NotNull(sent);
        // Без ключа идемпотентности повтор вернул бы клиенту деньги дважды.
        Assert.Equal(LastRefund!.Id, sent.IdempotenceKey);
        Assert.Equal(ProviderPaymentId, sent.ProviderPaymentId);
        // Валюта — из счёта: вернуть надо ровно то, что было принято.
        Assert.Equal("RUB", sent.Currency);
    }

    [Fact]
    public async Task Cancel_PaidInvoice_RecordsRefundBeforeCallingProvider()
    {
        SeedInvoice(InvoiceStatus.Paid, PaymentStatus.Succeeded);

        var refundRecorded = false;
        var recordedBeforeProviderCall = false;

        invoices.Setup(repository => repository.AddRefundAsync(It.IsAny<Refund>(), It.IsAny<CancellationToken>()))
            .Callback<Refund, CancellationToken>((refund, _) => { LastRefund = refund; refundRecorded = true; })
            .Returns(Task.CompletedTask);

        provider.Setup(client => client.RefundAsync(It.IsAny<ProviderRefundRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => recordedBeforeProviderCall = refundRecorded)
            .ReturnsAsync(ProviderRefundResult.Success("refund-1", ProviderOperationStatus.Succeeded, 1250.50m));

        await service.RefundForCancelledOrderAsync(orderId, "20260830-REFND1", null, default);

        // Сделанный у провайдера и не записанный возврат не отличить от несделанного.
        Assert.True(recordedBeforeProviderCall);
    }

    [Fact]
    public async Task Cancel_ProviderRejectsRefund_LeavesInvoicePaid()
    {
        SeedInvoice(InvoiceStatus.Paid, PaymentStatus.Succeeded);
        CaptureRefund();

        provider.Setup(client => client.RefundAsync(It.IsAny<ProviderRefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderRefundResult.Failure("provider is down"));

        await service.RefundForCancelledOrderAsync(orderId, "20260830-REFND1", null, default);

        invoices.Verify(
            repository => repository.MarkRefundFailedAsync(LastRefund!.Id, "provider is down", It.IsAny<CancellationToken>()),
            Times.Once);

        // Деньги всё ещё у платформы — объявлять их возвращёнными нельзя.
        invoices.Verify(
            repository => repository.MarkRefundSucceededAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        outbox.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Cancel_ProviderRefundStillPending_PublishesNothing()
    {
        SeedInvoice(InvoiceStatus.Paid, PaymentStatus.Succeeded);
        CaptureRefund();
        ProviderRefunds(ProviderOperationStatus.Pending);

        await service.RefundForCancelledOrderAsync(orderId, "20260830-REFND1", null, default);

        // Сказать «деньги вернулись», пока они в пути, значит соврать.
        outbox.VerifyNoOtherCalls();
        invoices.Verify(
            repository => repository.MarkRefundSucceededAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private Refund? LastRefund { get; set; }

    private Refund? CaptureRefund()
    {
        invoices.Setup(repository => repository.AddRefundAsync(It.IsAny<Refund>(), It.IsAny<CancellationToken>()))
            .Callback<Refund, CancellationToken>((refund, _) => LastRefund = refund)
            .Returns(Task.CompletedTask);

        return LastRefund;
    }

    private void ProviderRefunds(ProviderOperationStatus status) =>
        provider.Setup(client => client.RefundAsync(It.IsAny<ProviderRefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderRefundResult.Success("refund-1", status, 1250.50m));

    private Invoice SeedInvoice(InvoiceStatus invoiceStatus, PaymentStatus paymentStatus)
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
            OrderId = orderId,
            OrderNumber = "20260830-REFND1",
            Amount = 1250.50m,
            Currency = "RUB",
            Status = invoiceStatus,
            Payments = [payment],
        };

        payment.InvoiceId = invoice.Id;

        invoices.Setup(repository => repository.GetByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        return invoice;
    }
}
