using IdentityService.Application.Interfaces;
using IdentityService.Persistence.Outbox;
using IdentityService.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Persistence.Configuration;

public static class PersistenceConfiguration
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUsersRepository, UsersRepository>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<IOutboxReader, OutboxReader>();

        return services;
    }
}
