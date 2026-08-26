using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationService.Domain.Entities;

namespace NotificationService.Persistence.Configurations;

public class OrderRecipientConfiguration : IEntityTypeConfiguration<OrderRecipient>
{
    public void Configure(EntityTypeBuilder<OrderRecipient> builder)
    {
        builder.ToTable("order_recipients");

        // Ключ — идентификатор заявки: ровно по нему приходят все последующие события, и
        // повторная доставка OrderCreated тем самым безобидна (перезапишет ту же строку).
        builder.HasKey(orderRecipient => orderRecipient.OrderId);

        builder.Property(orderRecipient => orderRecipient.OrderId)
            .ValueGeneratedNever();

        builder.Property(orderRecipient => orderRecipient.RecipientUserId)
            .IsRequired();

        builder.Property(orderRecipient => orderRecipient.OrderNumber)
            .HasMaxLength(50)
            .IsRequired();
    }
}
