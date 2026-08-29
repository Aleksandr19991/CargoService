using AiInspectionService.Application.Interfaces;
using AiInspectionService.Infrastructure.Inference;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiInspectionService.Infrastructure.Configuration;

public static class ServicesConfiguration
{
    // Здесь же появятся consumer `CargoPhotoUploaded`, outbox-диспетчер для
    // `PackageIntegrityAssessed` и клиент file-storage-service — следующие задачи Фазы 7.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddInspectionModel(services, configuration);

        return services;
    }

    private static void AddInspectionModel(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(OnnxModelOptions.SectionName);
        var options = new OnnxModelOptions
        {
            Path = section["Path"],
            Version = section["Version"] ?? "unknown",
        };

        services.AddSingleton(options);

        // Singleton: InferenceSession держит веса в памяти и потокобезопасна на Run — создавать
        // её на запрос значило бы перечитывать модель с диска на каждый снимок.
        var modelFileExists = !string.IsNullOrWhiteSpace(options.Path) && File.Exists(options.Path);
        if (modelFileExists)
        {
            services.AddSingleton<IPackageInspectionModel, OnnxPackageInspectionModel>();
            return;
        }

        // Отсутствие файла модели — не ошибка конфигурации, а обычное состояние dev-стенда и
        // тестов (см. докблок StubPackageInspectionModel); заглушка сама пишет предупреждение на
        // каждый вердикт, так что принять её ответы за предсказания модели по логам невозможно.
        services.AddSingleton<IPackageInspectionModel, StubPackageInspectionModel>();
    }
}
