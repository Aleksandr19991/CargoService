using OrdersService.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace OrdersService.Application.Configuration;

public static class ServicesConfiguration
{
    public static void AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IOrdersService, OrdersService>();
    }
}
