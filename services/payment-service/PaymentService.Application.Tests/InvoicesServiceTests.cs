using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PaymentService.Application.Interfaces;
using PaymentService.Application.Models;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;

namespace PaymentService.Application.Tests;

/// <summary>
/// Выставление счёта по подтверждённой заявке. Провайдер везде — мок: настоящий эквайринг в
/// тестах не нужен, проверяются решения сервиса, а не чужой HTTP.
/// </summary>
public class InvoicesServiceTests
{
    private readonly Mock<IInvoicesRepository> invoices = new();
    private readonly Mock<IPaymentProviderClient> provider = new();
    private readonly InvoicesService service;

    public InvoicesServiceTests()
    {
        service = new InvoicesService(
            invoices.Object,
            provider.Object,
            new PaymentOptions { Currency = "RUB", VerifyWebhookWithProvider = true },
            NullLogger<InvoicesService>.Instance);
    }

    [Fact]
    public async Task IssueForConfirmedOrder_IssuesInvoiceWithPendingPayment()
    {
        Invoice? saved = null;
        invoices.Setup(repository => repository.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
            .Callback<Invoice, CancellationToken>((invoice, _) => saved = invoice)
            .Returns(Task.CompletedTask);

        ProviderPaymentSucceeds();

        await service.IssueForConfirmedOrderAsync(OrderId, "20260830-ABC123", 1250.50m, default);

        Assert.NotNull(saved);
        Assert.Equal(OrderId, saved.OrderId);
        Assert.Equal(1250.50m, saved.Amount);
        Assert.Equal("RUB", saved.Currency);
        Assert.Equal(InvoiceStatus.Issued, saved.Status);

        var payment = Assert.Single(saved.Payments);
        Assert.Equal(1250.50m, payment.Amount);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public async Task IssueForConfirmedOrder_UsesPaymentIdAsIdempotenceKey()
    {
        Invoice? saved = null;
        invoices.Setup(repository => repository.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
            .Callback<Invoice, CancellationToken>((invoice, _) => saved = invoice)
            .Returns(Task.CompletedTask);

        ProviderPaymentRequest? sent = null;
        provider.Setup(client => client.CreatePaymentAsync(It.IsAny<ProviderPaymentRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderPaymentRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(SuccessfulPayment);

        await service.IssueForConfirmedOrderAsync(OrderId, "20260830-ABC123", 1250.50m, default);

        // Ключ идемпотентности — идентификатор нашей записи платежа, а не свежий Guid: только так
        // повтор вызова (retry клиента, повторное событие) не заводит у провайдера второй платёж.
        Assert.NotNull(sent);
        Assert.Equal(saved!.Payments.Single().Id, sent.IdempotenceKey);
        Assert.Equal("RUB", sent.Currency);
    }

    [Fact]
    public async Task IssueForConfirmedOrder_SavesInvoiceBeforeCallingProvider()
    {
        var savedBeforeProviderCall = false;
        var invoiceSaved = false;

        invoices.Setup(repository => repository.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
            .Callback(() => invoiceSaved = true)
            .Returns(Task.CompletedTask);

        provider.Setup(client => client.CreatePaymentAsync(It.IsAny<ProviderPaymentRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => savedBeforeProviderCall = invoiceSaved)
            .ReturnsAsync(SuccessfulPayment);

        await service.IssueForConfirmedOrderAsync(OrderId, "20260830-ABC123", 1250.50m, default);

        // Обратный порядок оставил бы у провайдера платёж, о котором сервис ничего не знает.
        Assert.True(savedBeforeProviderCall);
    }

    [Fact]
    public async Task IssueForConfirmedOrder_StoresProviderPaymentAndConfirmationUrl()
    {
        Invoice? saved = null;
        invoices.Setup(repository => repository.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
            .Callback<Invoice, CancellationToken>((invoice, _) => saved = invoice)
            .Returns(Task.CompletedTask);

        ProviderPaymentSucceeds();

        await service.IssueForConfirmedOrderAsync(OrderId, "20260830-ABC123", 1250.50m, default);

        invoices.Verify(
            repository => repository.SetProviderPaymentAsync(
                saved!.Payments.Single().Id,
                "provider-1",
                "https://provider.example/pay",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IssueForConfirmedOrder_ProviderRejected_MarksPaymentFailed()
    {
        Invoice? saved = null;
        invoices.Setup(repository => repository.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
            .Callback<Invoice, CancellationToken>((invoice, _) => saved = invoice)
            .Returns(Task.CompletedTask);

        provider.Setup(client => client.CreatePaymentAsync(It.IsAny<ProviderPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderPaymentResult.Failure("provider is down"));

        await service.IssueForConfirmedOrderAsync(OrderId, "20260830-ABC123", 1250.50m, default);

        invoices.Verify(
            repository => repository.MarkPaymentFailedAsync(
                saved!.Payments.Single().Id,
                "provider is down",
                It.IsAny<CancellationToken>()),
            Times.Once);

        invoices.Verify(
            repository => repository.SetProviderPaymentAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task IssueForConfirmedOrder_NonPositiveAmount_IssuesNothing(decimal amount)
    {
        await service.IssueForConfirmedOrderAsync(OrderId, "20260830-ZERO00", amount, default);

        // Счёт не выставляется и не помечается оплаченным: подтверждённая заявка без стоимости —
        // ошибка вышестоящего расчёта, а не бесплатная перевозка.
        invoices.Verify(repository => repository.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()), Times.Never);
        provider.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task IssueForConfirmedOrder_InvoiceAlreadyExists_IssuesNothing()
    {
        invoices.Setup(repository => repository.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Invoice { Id = Guid.NewGuid(), OrderId = OrderId, OrderNumber = "20260830-ABC123" });

        await service.IssueForConfirmedOrderAsync(OrderId, "20260830-ABC123", 1250.50m, default);

        // Вторая линия защиты рядом с inbox: события с разными EventId по одной заявке.
        invoices.Verify(repository => repository.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()), Times.Never);
        provider.VerifyNoOtherCalls();
    }

    private static readonly Guid OrderId = Guid.NewGuid();

    private static ProviderPaymentResult SuccessfulPayment => ProviderPaymentResult.Success(
        "provider-1",
        ProviderOperationStatus.Pending,
        1250.50m,
        "https://provider.example/pay");

    private void ProviderPaymentSucceeds() =>
        provider.Setup(client => client.CreatePaymentAsync(It.IsAny<ProviderPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessfulPayment);
}
