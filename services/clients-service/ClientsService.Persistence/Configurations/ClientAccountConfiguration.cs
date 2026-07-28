using ClientsService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClientsService.Persistence.Configurations;

public class ClientAccountConfiguration : IEntityTypeConfiguration<ClientAccount>
{
    public void Configure(EntityTypeBuilder<ClientAccount> builder)
    {
        builder.ToTable("client_accounts");

        builder.HasKey(account => account.Id);

        builder.Property(account => account.UserId)
            .IsRequired();

        builder.HasIndex(account => account.UserId)
            .IsUnique();

        builder.HasMany(account => account.Counterparties)
            .WithOne()
            .HasForeignKey(counterparty => counterparty.ClientAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
