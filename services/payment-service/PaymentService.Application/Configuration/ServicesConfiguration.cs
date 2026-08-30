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

        // По обработчику на событие; обобщённый консьюмер в Infrastructure находит нужный по
        // типу события. `OrderCancelled` добавится в задаче 5 Фазы 9.
        services.AddScoped<IEventHandler<OrderConfirmed>, OrderConfirmedHandler>();
    }
}
