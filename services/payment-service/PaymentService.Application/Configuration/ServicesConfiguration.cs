using CargoService.Contracts.Events.V1;
using Microsoft.Extensions.DependencyInjection;
using PaymentService.Application.EventHandlers;
using PaymentService.Application.Interfaces;

namespace PaymentService.Application.Configuration;

public static class ServicesConfiguration
{
    public static void AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IInvoicesService, InvoicesService>();
        services.AddScoped<IPaymentWebhooksService, PaymentWebhooksService>();
        services.AddScoped<IRefundsService, RefundsService>();

        // По обработчику на событие; обобщённый консьюмер в Infrastructure находит нужный по
        // типу события.
        services.AddScoped<IEventHandler<OrderConfirmed>, OrderConfirmedHandler>();
        services.AddScoped<IEventHandler<OrderCancelled>, OrderCancelledHandler>();
    }
}
