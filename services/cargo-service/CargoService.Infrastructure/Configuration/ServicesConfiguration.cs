using CargoService.Application.Interfaces;
using CargoService.Infrastructure.FileStorage;
using CargoService.Infrastructure.Keycloak;
using CargoService.Infrastructure.Messaging;
using CargoService.Infrastructure.Outbox;
using CargoService.Infrastructure.Sla;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace CargoService.Infrastructure.Configuration;

public static class ServicesConfiguration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddFileStorageClient(services, configuration);

        var rabbitMqSection = configuration.GetSection(RabbitMqOptions.SectionName);
        var rabbitMqOptions = new RabbitMqOptions
        {
            HostName = rabbitMqSection["HostName"] ?? throw new InvalidOperationException("RabbitMQ:HostName is not configured."),
            Port = int.Parse(rabbitMqSection["Port"] ?? throw new InvalidOperationException("RabbitMQ:Port is not configured.")),
            UserName = rabbitMqSection["UserName"] ?? throw new InvalidOperationException("RabbitMQ:UserName is not configured."),
            Password = rabbitMqSection["Password"] ?? throw new InvalidOperationException("RabbitMQ:Password is not configured."),
        };

        services.AddSingleton(rabbitMqOptions);
        services.AddHostedService<OrderConfirmedConsumer>();
        services.AddHostedService<PackageIntegrityAssessedConsumer>();
        services.AddHostedService<OutboxDispatcher>();

        // Контроль SLA к RabbitMQ отношения не имеет — ходит только в БД, — но живёт здесь же,
        // потому что это такой же фоновый рабочий процесс сервиса.
        services.AddHostedService<SlaMonitor>();

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

        // Singleton, чтобы кэш токена был общим на весь процесс — иначе смысл кэширования теряется.
        services.AddHttpClient<ServiceTokenProvider>();

        var storageSection = configuration.GetSection(FileStorageClientOptions.SectionName);
        var storageOptions = new FileStorageClientOptions
        {
            BaseUrl = storageSection["BaseUrl"] ?? throw new InvalidOperationException("FileStorageService:BaseUrl is not configured."),
        };

        services.AddHttpClient<IFileStorageClient, FileStorageClient>(client =>
            {
                client.BaseAddress = new Uri(storageOptions.BaseUrl);
                // Как и у PricingClient в orders-service: общий таймаут — только предохранитель,
                // ограничивает попытку политика ниже, иначе он обрезал бы retry на середине.
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

    private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy() =>
        Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(5), TimeoutStrategy.Optimistic);
}
