using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.Interfaces;
using NotificationService.Persistence.Repositories;

namespace NotificationService.Persistence.Configuration;

public static class PersistenceConfiguration
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.EnableRetryOnFailure()));

        services.AddScoped<IRecipientsRepository, RecipientsRepository>();
        services.AddScoped<INotificationTemplatesRepository, NotificationTemplatesRepository>();
        services.AddScoped<INotificationLogsRepository, NotificationLogsRepository>();

        return services;
    }
}
