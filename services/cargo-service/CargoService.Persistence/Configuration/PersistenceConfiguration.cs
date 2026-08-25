using CargoService.Application.Interfaces;
using CargoService.Persistence.Outbox;
using CargoService.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CargoService.Persistence.Configuration;

public static class PersistenceConfiguration
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.EnableRetryOnFailure()));

        services.AddScoped<IShipmentsRepository, ShipmentsRepository>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<IOutboxReader, OutboxReader>();

        return services;
    }
}
