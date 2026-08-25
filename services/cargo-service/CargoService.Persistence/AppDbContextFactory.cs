using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CargoService.Persistence;

/// <summary>
/// Lets `dotnet ef` scaffold/apply migrations against this DbContext directly, without building
/// the full CargoService API host (see IdentityService.Persistence.AppDbContextFactory for why).
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Database is named cargoshipments, not cargoservice: the latter is already taken by
        // identity-service (a leftover from the platform-wide name predating this service).
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=cargoshipments;Username=cargoshipments;Password=cargoshipments";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.EnableRetryOnFailure());

        return new AppDbContext(optionsBuilder.Options);
    }
}
