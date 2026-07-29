using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OrdersService.Infrastructure.Configuration;

public static class ServicesConfiguration
{
    // No Infrastructure services yet — the RabbitMQ consumer for TariffChanged and the outbox
    // publisher for OrderCreated/OrderConfirmed/OrderCancelled (see spec.md Phase 4) are wired up
    // here once they're built, following IdentityService.Infrastructure's OutboxDispatcher as the
    // reference for BackgroundService/reconnect-on-failure style.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }
}
