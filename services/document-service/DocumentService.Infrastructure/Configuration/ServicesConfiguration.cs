using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentService.Infrastructure.Configuration;

public static class ServicesConfiguration
{
    // No Infrastructure services yet. Здесь будут внешние границы сервиса (см. Фазу 8 в
    // spec.md): генератор PDF и штрихкодов за интерфейсом Application (чтобы вёрстку можно
    // было заменить, а тесты гонять без неё), клиент file-storage-service для загрузки готовых
    // файлов, консьюмеры `CargoAccepted`/`CargoDelivered` и outbox для `DocumentGenerated` —
    // сервис одновременно потребитель и издатель, как ai-inspection-service.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }
}
