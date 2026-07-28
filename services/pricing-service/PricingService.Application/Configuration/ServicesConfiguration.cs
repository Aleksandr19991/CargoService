using PricingService.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace PricingService.Application.Configuration;

public static class ServicesConfiguration
{
    public static void AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IPricingCalculationService, PricingCalculationService>();
    }
}
