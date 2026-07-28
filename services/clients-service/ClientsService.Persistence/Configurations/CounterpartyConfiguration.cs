using ClientsService.Domain.Entities;
using ClientsService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClientsService.Persistence.Configurations;

public class CounterpartyConfiguration : IEntityTypeConfiguration<Counterparty>
{
    public void Configure(EntityTypeBuilder<Counterparty> builder)
    {
        builder.ToTable("counterparties");

        builder.HasKey(counterparty => counterparty.Id);

        builder.Property(counterparty => counterparty.ClientAccountId)
            .IsRequired();

        builder.Property(counterparty => counterparty.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(counterparty => counterparty.OrganizationName)
            .HasMaxLength(200);

        builder.Property(counterparty => counterparty.FullName)
            .HasMaxLength(200);

        builder.Property(counterparty => counterparty.Inn)
            .HasMaxLength(12);

        builder.Property(counterparty => counterparty.City)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(counterparty => counterparty.Phone)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(counterparty => counterparty.Email)
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex(counterparty => counterparty.City);
        builder.HasIndex(counterparty => counterparty.Phone);
        builder.HasIndex(counterparty => counterparty.OrganizationName);
        builder.HasIndex(counterparty => counterparty.FullName);
    }
}
