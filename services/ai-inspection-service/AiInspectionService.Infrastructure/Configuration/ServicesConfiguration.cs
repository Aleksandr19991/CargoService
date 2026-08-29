using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiInspectionService.Infrastructure.Configuration;

public static class ServicesConfiguration
{
    // No Infrastructure services yet. Здесь будут жить все внешние границы сервиса (см. Фазу 7
    // в spec.md): consumer `CargoPhotoUploaded`, outbox-диспетчер для `PackageIntegrityAssessed`,
    // клиент file-storage-service (фото для инференса) и сам инференс — за интерфейсом
    // Application, чтобы модель можно было подменить (ONNX локально либо внешний CV API), а
    // тесты гонять на её заглушке.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }
}
