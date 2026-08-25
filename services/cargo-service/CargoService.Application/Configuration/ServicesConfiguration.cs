using CargoService.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CargoService.Application.Configuration;

public static class ServicesConfiguration
{
    public static void AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IShipmentsService, ShipmentsService>();
    }
}
