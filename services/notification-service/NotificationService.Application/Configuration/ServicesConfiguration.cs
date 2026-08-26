using CargoService.Contracts.Events.V1;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.EventHandlers;
using NotificationService.Application.Interfaces;

namespace NotificationService.Application.Configuration;

public static class ServicesConfiguration
{
    public static void AddApplicationServices(this IServiceCollection services)
    {
        // Scoped, а не singleton: среди отправителей есть типизированный HttpClient (SMS), а он
        // регистрируется transient — захват его синглтоном заморозил бы HttpMessageHandler на
        // весь процесс, мимо ротации, которую делает IHttpClientFactory.
        services.AddScoped<INotificationSenderRegistry, NotificationSenderRegistry>();

        services.AddScoped<INotificationsService, NotificationsService>();

        AddEventHandlers(services);
    }

    /// <summary>
    /// По обработчику на каждое событие из таблицы подписок (spec.md §4). Обобщённый консьюмер
    /// в Infrastructure находит нужный по типу события — добавление события сводится к новому
    /// обработчику здесь и одной строке регистрации консьюмера.
    /// </summary>
    private static void AddEventHandlers(IServiceCollection services)
    {
        // Не уведомление, а наполнение read-модели контактов.
        services.AddScoped<IEventHandler<UserRegistered>, UserRegisteredHandler>();

        services.AddScoped<IEventHandler<OrderCreated>, OrderCreatedHandler>();
        services.AddScoped<IEventHandler<OrderConfirmed>, OrderConfirmedHandler>();
        services.AddScoped<IEventHandler<OrderCancelled>, OrderCancelledHandler>();

        services.AddScoped<IEventHandler<CargoAccepted>, CargoAcceptedHandler>();
        services.AddScoped<IEventHandler<CargoStatusChanged>, CargoStatusChangedHandler>();
        services.AddScoped<IEventHandler<CargoDelivered>, CargoDeliveredHandler>();

        services.AddScoped<IEventHandler<PaymentCompleted>, PaymentCompletedHandler>();
        services.AddScoped<IEventHandler<PaymentFailed>, PaymentFailedHandler>();

        services.AddScoped<IEventHandler<DocumentGenerated>, DocumentGeneratedHandler>();

        services.AddScoped<IEventHandler<PackageIntegrityAssessed>, PackageIntegrityAssessedHandler>();
    }
}
