using PricingService.Application.Interfaces;
using PricingService.Persistence.Outbox;
using PricingService.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PricingService.Persistence.Configuration;

public static class PersistenceConfiguration
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.EnableRetryOnFailure()));

        services.AddScoped<ITariffRatesRepository, TariffRatesRepository>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<IOutboxReader, OutboxReader>();

        return services;
    }
}
