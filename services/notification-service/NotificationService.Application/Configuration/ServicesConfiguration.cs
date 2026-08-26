using Microsoft.Extensions.DependencyInjection;

namespace NotificationService.Application.Configuration;

public static class ServicesConfiguration
{
    // No Application services yet — use-case services are registered here as they're added
    // (see CargoService.Application/Configuration/ServicesConfiguration.cs for the pattern).
    public static void AddApplicationServices(this IServiceCollection services)
    {
    }
}
