using CargoService.Domain.Entities;
using CargoService.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace CargoService.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<AcceptanceInspection> AcceptanceInspections => Set<AcceptanceInspection>();
    public DbSet<PackagingService> PackagingServices => Set<PackagingService>();
    public DbSet<ShipmentStatusHistory> ShipmentStatusHistory => Set<ShipmentStatusHistory>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
