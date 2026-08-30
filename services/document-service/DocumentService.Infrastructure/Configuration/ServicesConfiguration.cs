using CargoService.Contracts.Events.V1;
using DocumentService.Application.Interfaces;
using DocumentService.Infrastructure.FileStorage;
using DocumentService.Infrastructure.Keycloak;
using DocumentService.Infrastructure.Messaging;
using DocumentService.Infrastructure.Outbox;
using DocumentService.Infrastructure.Pdf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using QuestPDF;
using QuestPDF.Infrastructure;

namespace DocumentService.Infrastructure.Configuration;

public static class ServicesConfiguration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddTrackingCodes(services, configuration);
        AddPdfRenderer(services);
        AddFileStorageClient(services, configuration);
        AddEventConsumers(services, configuration);

        services.AddHostedService<DocumentGenerationWorker>();
        services.AddHostedService<OutboxDispatcher>();

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
        };

        services.AddSingleton(storageOptions);

        services.AddHttpClient<IFileStorageClient, FileStorageClient>(client =>
            {
                client.BaseAddress = new Uri(storageOptions.BaseUrl);
                // Как и у остальных межсервисных клиентов: общий таймаут — предохранитель,
                // отдельную попытку ограничивает политика ниже.
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

    // Загрузка файла — не мгновенная операция, поэтому лимит попытки больше, чем у обычных
    // JSON-вызовов в других сервисах.
    private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy() =>
        Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30), TimeoutStrategy.Optimistic);

    /// <summary>
    /// Четыре подписки: события груза говорят, какие документы понадобились, события заявки
    /// наполняют сведения для печати (см. <c>OrderSnapshot</c>). Обобщённый консьюмер, как в
    /// notification-service: подписок больше одной, и копии одной топологии очередей разошлись бы.
    /// </summary>
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

        services.AddEventConsumer<OrderCreated>("orders-service");
        services.AddEventConsumer<OrderConfirmed>("orders-service");
        services.AddEventConsumer<CargoAccepted>("cargo-service");
        services.AddEventConsumer<CargoDelivered>("cargo-service");
    }

    private static void AddEventConsumer<TEvent>(this IServiceCollection services, string publishingService)
        where TEvent : IntegrationEvent =>
        services.AddHostedService(provider => new EventConsumer<TEvent>(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<RabbitMqOptions>(),
            publishingService,
            provider.GetRequiredService<ILogger<EventConsumer<TEvent>>>()));

    private static void AddTrackingCodes(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(TrackingCodeOptions.SectionName);

        services.AddSingleton(new TrackingCodeOptions
        {
            PublicTrackingBaseUrl = section["PublicTrackingBaseUrl"],
        });

        services.AddSingleton<ITrackingCodeGenerator, QrTrackingCodeGenerator>();
    }

    private static void AddPdfRenderer(IServiceCollection services)
    {
        // QuestPDF требует явно объявить лицензию, под которой используется. Community —
        // бесплатная лицензия для открытых проектов и организаций с выручкой до $1 млн;
        // при выходе за эти рамки её нужно сменить на коммерческую (или заменить саму
        // библиотеку — вёрстка изолирована в IDocumentRenderer ровно для этого).
        Settings.License = LicenseType.Community;

        // Отсутствующий глиф должен ломать генерацию, а не печататься пустым прямоугольником:
        // документ с «квадратами» вместо кириллицы выглядит как рабочий и уходит клиенту.
        Settings.CheckIfAllTextGlyphsAreAvailable = true;

        // Системные шрифты не используются: сервис печатает одинаково всюду, а в образе
        // dotnet/aspnet их и нет вовсе (см. докблок DocumentFonts).
        Settings.UseEnvironmentFonts = false;

        // Singleton: рендерер не хранит состояния запроса, а регистрация шрифтов при его
        // создании — разовая работа, которую незачем повторять на каждый документ.
        services.AddSingleton<IDocumentRenderer, QuestPdfDocumentRenderer>();
    }
}
