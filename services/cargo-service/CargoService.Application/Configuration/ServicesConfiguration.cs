using Microsoft.Extensions.DependencyInjection;

namespace CargoService.Application.Configuration;

public static class ServicesConfiguration
{
    // No Application services yet — use-case services are registered here as they're added
    // (see IdentityService.Application/Configuration/ServicesConfiguration.cs for the pattern).
    public static void AddApplicationServices(this IServiceCollection services)
    {
    }
}
