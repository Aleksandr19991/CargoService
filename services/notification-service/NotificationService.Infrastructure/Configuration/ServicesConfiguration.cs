using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NotificationService.Infrastructure.Configuration;

public static class ServicesConfiguration
{
    // No Infrastructure services yet. Здесь будут жить обе внешние границы сервиса (см. Фазу 6
    // в spec.md): RabbitMQ-консьюмеры статусных событий (OrderCreated, OrderConfirmed,
    // CargoAccepted, CargoStatusChanged, PaymentCompleted, DocumentGenerated) — по образцу
    // консьюмеров cargo-service (BackgroundService со своей очередью/DLQ и переподключением),
    // и отправители уведомлений (SMTP/SendGrid, SMS) за интерфейсами Application.
    // Outbox здесь не предполагается: notification-service — конечный потребитель событий,
    // собственных он пока не публикует.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }
}
