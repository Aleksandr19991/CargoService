using AiInspectionService.Application.Interfaces;
using AiInspectionService.Infrastructure.FileStorage;
using AiInspectionService.Infrastructure.Inference;
using AiInspectionService.Infrastructure.Keycloak;
using AiInspectionService.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace AiInspectionService.Infrastructure.Configuration;

public static class ServicesConfiguration
{
    // Здесь же появится outbox-диспетчер для `PackageIntegrityAssessed` — следующая задача Фазы 7.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddInspectionModel(services, configuration);
        AddFileStorageClient(services, configuration);
        AddEventConsumers(services, configuration);

        services.AddHostedService<InspectionWorker>();

        return services;
    }

    private static void AddFileStorageClient(IServiceCollection services, IConfiguration configuration)
    {
        var keycloakSection = configuration.GetSection(KeycloakServiceAccountOptions.SectionName);
        var accountOptions = new KeycloakServiceAccountOptions
        {
            BaseUrl = keycloakSection["BaseUrl"] ?? throw new InvalidOperationException("KeycloakServiceAccount:BaseUrl is not configured."),
            Realm = keycloakSection["Realm"] ?? throw new InvalidOperationException("KeycloakServiceAccount:Realm is not configured."),
            ClientId = keycloakSection["ClientId"] ?? throw new InvalidOperationException("KeycloakServiceAccount:ClientId is not configured."),
            ClientSecret = keycloakSection["ClientSecret"] ?? throw new InvalidOperationException("KeycloakServiceAccount:ClientSecret is not configured."),
        };

        services.AddSingleton(accountOptions);

        // Кэш токена общий на процесс — иначе смысл кэширования теряется.
        services.AddHttpClient<ServiceTokenProvider>();

        var storageSection = configuration.GetSection(FileStorageClientOptions.SectionName);
        var storageOptions = new FileStorageClientOptions
        {
            BaseUrl = storageSection["BaseUrl"] ?? throw new InvalidOperationException("FileStorageService:BaseUrl is not configured."),
            UseInternalUrls = !bool.TryParse(storageSection["UseInternalUrls"], out var useInternal) || useInternal,
        };

        services.AddSingleton(storageOptions);

        services.AddHttpClient<IFileStorageClient, FileStorageClient>(client =>
            {
                client.BaseAddress = new Uri(storageOptions.BaseUrl);
                // Как и у клиентов в orders/cargo: общий таймаут — предохранитель, отдельную
                // попытку ограничивает политика ниже, иначе он обрежет retry на середине.
                client.Timeout = TimeSpan.FromSeconds(60);
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy())
            .AddPolicyHandler(GetTimeoutPolicy());
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

    // Скачивание снимка — не мгновенная операция, поэтому лимит попытки больше, чем у обычных
    // JSON-вызовов в других сервисах.
    private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy() =>
        Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30), TimeoutStrategy.Optimistic);

    private static void AddEventConsumers(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(RabbitMqOptions.SectionName);
        var options = new RabbitMqOptions
        {
            HostName = section["HostName"] ?? throw new InvalidOperationException("RabbitMQ:HostName is not configured."),
            Port = int.Parse(section["Port"] ?? throw new InvalidOperationException("RabbitMQ:Port is not configured.")),
            UserName = section["UserName"] ?? throw new InvalidOperationException("RabbitMQ:UserName is not configured."),
            Password = section["Password"] ?? throw new InvalidOperationException("RabbitMQ:Password is not configured."),
        };

        services.AddSingleton(options);
        services.AddHostedService<CargoPhotoUploadedConsumer>();
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
