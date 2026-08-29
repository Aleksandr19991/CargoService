using AiInspectionService.Application.Interfaces;
using AiInspectionService.Persistence.Inbox;
using AiInspectionService.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiInspectionService.Persistence.Configuration;

public static class PersistenceConfiguration
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.EnableRetryOnFailure()));

        services.AddScoped<IInspectionJobsRepository, InspectionJobsRepository>();
        services.AddScoped<IInboxRepository, InboxRepository>();

        return services;
    }
}
