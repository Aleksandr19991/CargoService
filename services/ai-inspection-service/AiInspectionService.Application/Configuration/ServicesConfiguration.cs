using AiInspectionService.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AiInspectionService.Application.Configuration;

public static class ServicesConfiguration
{
    public static void AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IInspectionJobsService, InspectionJobsService>();
        services.AddScoped<IInspectionProcessor, InspectionProcessor>();
    }
}
