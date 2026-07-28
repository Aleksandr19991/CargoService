using ClientsService.Application.Interfaces;
using ClientsService.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClientsService.Persistence.Configuration;

public static class PersistenceConfiguration
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.EnableRetryOnFailure()));

        services.AddScoped<IClientAccountsRepository, ClientAccountsRepository>();
        services.AddScoped<ICounterpartiesRepository, CounterpartiesRepository>();

        return services;
    }
}
