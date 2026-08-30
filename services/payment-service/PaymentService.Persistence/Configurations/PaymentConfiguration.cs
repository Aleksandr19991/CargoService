using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;

namespace PaymentService.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.InvoiceId).IsRequired();

        builder.Property(payment => payment.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(payment => payment.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(PaymentStatus.Pending)
            .IsRequired();

        builder.Property(payment => payment.ProviderPaymentId).HasMaxLength(100);
        builder.Property(payment => payment.ConfirmationUrl).HasMaxLength(2000);
        builder.Property(payment => payment.FailureReason).HasMaxLength(1000);

        builder.Property(payment => payment.CreatedAt).IsRequired();

        // Главный способ найти платёж — идентификатор транзакции из webhook провайдера. Индекс
        // уникальный и частичный: null'ов здесь столько же, сколько платежей, которые провайдеру
        // завести не удалось, и уникальность на них не распространяется.
        builder.HasIndex(payment => payment.ProviderPaymentId)
            .IsUnique()
            .HasFilter("\"ProviderPaymentId\" IS NOT NULL");

        builder.HasIndex(payment => payment.InvoiceId);
    }
}
