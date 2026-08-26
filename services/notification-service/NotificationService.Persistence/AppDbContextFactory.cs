using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NotificationService.Persistence;

/// <summary>
/// Lets `dotnet ef` scaffold/apply migrations against this DbContext directly, without building
/// the full NotificationService API host (see IdentityService.Persistence.AppDbContextFactory for why).
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=notificationservice;Username=notificationservice;Password=notificationservice";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.EnableRetryOnFailure());

        return new AppDbContext(optionsBuilder.Options);
    }
}
