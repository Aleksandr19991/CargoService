using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CargoService.Infrastructure.Configuration;

public static class ServicesConfiguration
{
    // No Infrastructure services yet — the RabbitMQ consumers (OrderConfirmed, PackageIntegrityAssessed)
    // and the outbox publisher for CargoAccepted/CargoStatusChanged/CargoPhotoUploaded/CargoDelivered
    // (see spec.md Phase 5) are wired up here once they're built, following
    // OrdersService.Infrastructure's OutboxDispatcher/consumers as the reference for
    // BackgroundService/reconnect-on-failure style.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }
}
