using ClientsService.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ClientsService.Application.Configuration;

public static class ServicesConfiguration
{
    public static void AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ICounterpartiesService, CounterpartiesService>();
    }
}
