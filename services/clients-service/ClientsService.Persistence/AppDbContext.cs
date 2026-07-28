using ClientsService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClientsService.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ClientAccount> ClientAccounts => Set<ClientAccount>();
    public DbSet<Counterparty> Counterparties => Set<Counterparty>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
