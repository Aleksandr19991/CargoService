using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;

namespace PaymentService.Persistence.Repositories;

public class InvoicesRepository(AppDbContext dbContext) : IInvoicesRepository
{
    public Task<Invoice?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        dbContext.Invoices
            .AsNoTracking()
            .Include(invoice => invoice.Payments)
            .FirstOrDefaultAsync(invoice => invoice.OrderId == orderId, cancellationToken);

    public Task<Invoice?> GetByProviderPaymentIdAsync(string providerPaymentId, CancellationToken cancellationToken) =>
        dbContext.Invoices
            .AsNoTracking()
            .Include(invoice => invoice.Payments)
            .FirstOrDefaultAsync(
                invoice => invoice.Payments.Any(payment => payment.ProviderPaymentId == providerPaymentId),
                cancellationToken);

    public async Task AddAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        // Платежи едут вместе со счётом: EF обходит граф от корня, и обе вставки уходят одной
        // транзакцией — счёта без платежа в базе не появится.
        await dbContext.Invoices.AddAsync(invoice, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetProviderPaymentAsync(
        Guid paymentId,
        string providerPaymentId,
        string? confirmationUrl,
        CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments.FirstAsync(stored => stored.Id == paymentId, cancellationToken);

        payment.ProviderPaymentId = providerPaymentId;
        payment.ConfirmationUrl = confirmationUrl;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkPaymentSucceededAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments.FirstAsync(stored => stored.Id == paymentId, cancellationToken);
        var invoice = await dbContext.Invoices.FirstAsync(stored => stored.Id == payment.InvoiceId, cancellationToken);

        var now = DateTimeOffset.UtcNow;

        payment.Status = PaymentStatus.Succeeded;
        payment.CompletedAt = now;

        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAt = now;

        // SaveChanges здесь один на всё: событие в outbox поставлено вызывающим кодом на этом же
        // DbContext, поэтому платёж, счёт и событие коммитятся одной транзакцией.
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkPaymentFailedAsync(Guid paymentId, string reason, CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments.FirstAsync(stored => stored.Id == paymentId, cancellationToken);

        payment.Status = PaymentStatus.Failed;
        payment.FailureReason = reason;
        payment.CompletedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
