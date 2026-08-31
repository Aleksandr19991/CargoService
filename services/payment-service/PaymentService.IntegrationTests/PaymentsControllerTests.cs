using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Models;
using PaymentService.Domain.Enums;

namespace PaymentService.IntegrationTests;

/// <summary>
/// Приём уведомлений провайдера поверх настоящего API и настоящей БД. Проверяются коды ответа и
/// то, что реально легло в таблицы — включая строку outbox, без которой об оплате никто бы не узнал.
/// </summary>
public class PaymentsControllerTests(PaymentApiFactory factory) : IClassFixture<PaymentApiFactory>
{
    private const string WebhookUrl = "api/payments/webhook";

    [Fact]
    public async Task Webhook_MalformedBody_ReturnsBadRequest()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(WebhookUrl, new { @event = "payment.succeeded" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_UnknownPayment_ReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(WebhookUrl, Notification("provider-unknown", "succeeded"));

        // Повтор ничего не изменит — провайдеру незачем присылать это уведомление снова.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_Succeeded_MarksInvoicePaidAndStagesPaymentCompleted()
    {
        var providerPaymentId = NewProviderPaymentId();
        var (invoice, payment) = await factory.SeedInvoiceAsync(providerPaymentId);
        factory.Provider.PaymentStatus = ProviderOperationStatus.Succeeded;

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(WebhookUrl, Notification(providerPaymentId, "succeeded"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = factory.CreateDbContext();
        var stored = await context.Invoices.AsNoTracking().FirstAsync(row => row.Id == invoice.Id);
        var storedPayment = await context.Payments.AsNoTracking().FirstAsync(row => row.Id == payment.Id);

        Assert.Equal(InvoiceStatus.Paid, stored.Status);
        Assert.NotNull(stored.PaidAt);
        Assert.Equal(PaymentStatus.Succeeded, storedPayment.Status);

        var message = Assert.Single(await StagedForAsync(context, invoice.OrderId));
        Assert.Equal("payment-service.payment-completed", message.RoutingKey);
    }

    [Fact]
    public async Task Webhook_DeliveredTwice_StagesSingleEvent()
    {
        var providerPaymentId = NewProviderPaymentId();
        var (invoice, _) = await factory.SeedInvoiceAsync(providerPaymentId);
        factory.Provider.PaymentStatus = ProviderOperationStatus.Succeeded;

        var client = factory.CreateClient();
        await client.PostAsJsonAsync(WebhookUrl, Notification(providerPaymentId, "succeeded"));
        var repeated = await client.PostAsJsonAsync(WebhookUrl, Notification(providerPaymentId, "succeeded"));

        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);

        await using var context = factory.CreateDbContext();

        // Провайдеры доставляют уведомления at-least-once — второй PaymentCompleted означал бы
        // вторую отметку об оплате у orders-service.
        Assert.Single(await StagedForAsync(context, invoice.OrderId));
    }

    [Fact]
    public async Task Webhook_Canceled_MarksPaymentFailedAndLeavesInvoiceIssued()
    {
        var providerPaymentId = NewProviderPaymentId();
        var (invoice, payment) = await factory.SeedInvoiceAsync(providerPaymentId);
        factory.Provider.PaymentStatus = ProviderOperationStatus.Canceled;
        factory.Provider.CancellationReason = "insufficient_funds";

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(WebhookUrl, Notification(providerPaymentId, "canceled"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = factory.CreateDbContext();
        var stored = await context.Invoices.AsNoTracking().FirstAsync(row => row.Id == invoice.Id);
        var storedPayment = await context.Payments.AsNoTracking().FirstAsync(row => row.Id == payment.Id);

        // Счёт остаётся выставленным: клиент вправе попробовать оплатить снова.
        Assert.Equal(InvoiceStatus.Issued, stored.Status);
        Assert.Equal(PaymentStatus.Failed, storedPayment.Status);
        Assert.Contains("insufficient_funds", storedPayment.FailureReason);

        var message = Assert.Single(await StagedForAsync(context, invoice.OrderId));
        Assert.Equal("payment-service.payment-failed", message.RoutingKey);

        factory.Provider.CancellationReason = null;
    }

    [Fact]
    public async Task Webhook_ProviderContradictsPayload_LeavesInvoiceUnpaid()
    {
        var providerPaymentId = NewProviderPaymentId();
        var (invoice, _) = await factory.SeedInvoiceAsync(providerPaymentId);
        factory.Provider.PaymentStatus = ProviderOperationStatus.Pending;

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(WebhookUrl, Notification(providerPaymentId, "succeeded"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = factory.CreateDbContext();
        var stored = await context.Invoices.AsNoTracking().FirstAsync(row => row.Id == invoice.Id);

        // Знание идентификатора платежа не даёт объявить заявку оплаченной.
        Assert.Equal(InvoiceStatus.Issued, stored.Status);
        Assert.Empty(await StagedForAsync(context, invoice.OrderId));
    }

    [Fact]
    public async Task Webhook_ProviderUnavailable_ReturnsServiceUnavailable()
    {
        var providerPaymentId = NewProviderPaymentId();
        var (invoice, _) = await factory.SeedInvoiceAsync(providerPaymentId);
        factory.Provider.IsUnavailable = true;

        try
        {
            var client = factory.CreateClient();
            var response = await client.PostAsJsonAsync(WebhookUrl, Notification(providerPaymentId, "succeeded"));

            // Не-2xx — просьба прислать уведомление снова, когда провайдер ответит.
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        finally
        {
            factory.Provider.IsUnavailable = false;
        }

        await using var context = factory.CreateDbContext();
        var stored = await context.Invoices.AsNoTracking().FirstAsync(row => row.Id == invoice.Id);
        Assert.Equal(InvoiceStatus.Issued, stored.Status);
    }

    [Fact]
    public async Task Webhook_SucceededAfterCancellation_RefundsInsteadOfPayingInvoice()
    {
        var providerPaymentId = NewProviderPaymentId();
        var (invoice, payment) = await factory.SeedInvoiceAsync(providerPaymentId, InvoiceStatus.Cancelled);
        factory.Provider.PaymentStatus = ProviderOperationStatus.Succeeded;

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(WebhookUrl, Notification(providerPaymentId, "succeeded"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = factory.CreateDbContext();
        var stored = await context.Invoices.AsNoTracking().FirstAsync(row => row.Id == invoice.Id);
        var storedPayment = await context.Payments.AsNoTracking().FirstAsync(row => row.Id == payment.Id);
        var refund = await context.Refunds.AsNoTracking().FirstAsync(row => row.InvoiceId == invoice.Id);

        Assert.Equal(PaymentStatus.Succeeded, storedPayment.Status);
        Assert.Equal(InvoiceStatus.Refunded, stored.Status);
        Assert.Equal(RefundStatus.Succeeded, refund.Status);
        Assert.Equal(payment.Amount, refund.Amount);

        // Наружу уходит только возврат: заявка отменена, и «оплачена» о ней говорить нельзя.
        var message = Assert.Single(await StagedForAsync(context, invoice.OrderId));
        Assert.Equal("payment-service.refund-issued", message.RoutingKey);
    }

    /// <summary>
    /// Строки outbox по конкретной заявке. Фильтр по подстроке считается в памяти: колонка
    /// <c>Payload</c> имеет тип jsonb, и <c>string.Contains</c> над ней Npgsql переводит в
    /// <c>jsonb ~~ jsonb</c> — такого оператора в Postgres нет, запрос падает с 42883.
    /// В тестовой БД строк единицы, так что выгрузить их целиком дешевле, чем городить
    /// приведение типа в запросе.
    /// </summary>
    private static async Task<List<Persistence.Outbox.OutboxMessage>> StagedForAsync(
        Persistence.AppDbContext context,
        Guid orderId)
    {
        var messages = await context.OutboxMessages.AsNoTracking().ToListAsync();

        return messages.Where(message => message.Payload.Contains(orderId.ToString())).ToList();
    }

    private static string NewProviderPaymentId() => $"provider-{Guid.NewGuid():N}";

    private static object Notification(string providerPaymentId, string status) => new
    {
        @event = $"payment.{status}",
        @object = new { id = providerPaymentId, status },
    };
}
