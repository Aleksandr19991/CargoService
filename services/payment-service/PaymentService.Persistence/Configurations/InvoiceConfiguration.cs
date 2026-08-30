using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;

namespace PaymentService.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");

        builder.HasKey(invoice => invoice.Id);

        builder.Property(invoice => invoice.OrderId).IsRequired();

        builder.Property(invoice => invoice.OrderNumber)
            .HasMaxLength(50)
            .IsRequired();

        // Деньги — numeric с фиксированной точностью, а не double: округление в двоичной
        // плавающей точке на суммах недопустимо.
        builder.Property(invoice => invoice.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(invoice => invoice.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(invoice => invoice.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(InvoiceStatus.Issued)
            .IsRequired();

        builder.Property(invoice => invoice.CreatedAt).IsRequired();

        // Счёт по заявке ровно один: он и ищется по заявке (консьюмер проверяет, не выставлен ли
        // уже), и не должен задваиваться при повторной доставке события — уникальность здесь
        // страхует inbox на уровне схемы, а не только логики.
        builder.HasIndex(invoice => invoice.OrderId).IsUnique();

        builder.HasMany(invoice => invoice.Payments)
            .WithOne()
            .HasForeignKey(payment => payment.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
