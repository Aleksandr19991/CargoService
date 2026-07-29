using Microsoft.EntityFrameworkCore;
using PricingService.Domain.Entities;
using PricingService.Persistence.Outbox;

namespace PricingService.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TariffRate> TariffRates => Set<TariffRate>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
