using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;

namespace PaymentService.Persistence.Configurations;

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("refunds");

        builder.HasKey(refund => refund.Id);

        builder.Property(refund => refund.InvoiceId).IsRequired();
        builder.Property(refund => refund.PaymentId).IsRequired();

        builder.Property(refund => refund.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(refund => refund.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(RefundStatus.Pending)
            .IsRequired();

        builder.Property(refund => refund.ProviderRefundId).HasMaxLength(100);

        builder.Property(refund => refund.Reason)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(refund => refund.FailureReason).HasMaxLength(1000);

        builder.Property(refund => refund.CreatedAt).IsRequired();

        builder.HasIndex(refund => refund.PaymentId);

        // Уникальности «один возврат на платёж» нет: провайдеры допускают частичный возврат, и
        // несколько возвратов по одному платежу — законный случай. От повторного возврата по
        // отменённой заявке защищает статус счёта, а не схема.
    }
}
