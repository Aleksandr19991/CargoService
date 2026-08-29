using DocumentService.Application.Interfaces;
using DocumentService.Persistence.Inbox;
using DocumentService.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentService.Persistence.Configuration;

public static class PersistenceConfiguration
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.EnableRetryOnFailure()));

        services.AddScoped<IDocumentsRepository, DocumentsRepository>();
        services.AddScoped<IOrderSnapshotsRepository, OrderSnapshotsRepository>();
        services.AddScoped<IInboxRepository, InboxRepository>();

        return services;
    }
}
