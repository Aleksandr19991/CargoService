using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PricingService.Infrastructure.Configuration;

public static class ServicesConfiguration
{
    // No Infrastructure services yet — publishing TariffChanged over RabbitMQ (see spec.md
    // Phase 3) is wired up here once it's built, following IdentityService.Infrastructure's
    // OutboxDispatcher as the reference for BackgroundService/reconnect-on-failure style.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }
}
