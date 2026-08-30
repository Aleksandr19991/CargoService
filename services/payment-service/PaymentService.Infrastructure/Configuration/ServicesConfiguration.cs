using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PaymentService.Infrastructure.Configuration;

public static class ServicesConfiguration
{
    // No Infrastructure services yet. Здесь будут внешние границы сервиса (см. Фазу 9 в
    // spec.md): клиент платёжного провайдера за интерфейсом Application (чтобы эквайринг можно
    // было заменить, а тесты гонять на моке провайдера), консьюмеры `OrderConfirmed`/`OrderCancelled`
    // и outbox для `PaymentCompleted`/`PaymentFailed`/`RefundIssued` — сервис одновременно
    // потребитель и издатель, как document-service.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }
}
