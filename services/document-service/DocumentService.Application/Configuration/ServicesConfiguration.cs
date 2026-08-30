using CargoService.Contracts.Events.V1;
using DocumentService.Application.EventHandlers;
using DocumentService.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentService.Application.Configuration;

public static class ServicesConfiguration
{
    public static void AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IDocumentsService, DocumentsService>();
        services.AddScoped<IDocumentGenerationProcessor, DocumentGenerationProcessor>();
        services.AddScoped<IDocumentsQueryService, DocumentsQueryService>();

        // По обработчику на событие; обобщённый консьюмер в Infrastructure находит нужный по
        // типу события.
        services.AddScoped<IEventHandler<OrderCreated>, OrderCreatedHandler>();
        services.AddScoped<IEventHandler<OrderConfirmed>, OrderConfirmedHandler>();
        services.AddScoped<IEventHandler<CargoAccepted>, CargoAcceptedHandler>();
        services.AddScoped<IEventHandler<CargoDelivered>, CargoDeliveredHandler>();
    }
}
