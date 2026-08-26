using CargoService.Contracts.Events.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Models;
using NotificationService.Infrastructure.Email;
using NotificationService.Infrastructure.Messaging;
using NotificationService.Infrastructure.Sms;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace NotificationService.Infrastructure.Configuration;

public static class ServicesConfiguration
{
    // Outbox здесь не предполагается: сервис — конечный потребитель событий и своих не публикует.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddNotificationOptions(services, configuration);
        AddEmailSender(services, configuration);
        AddSmsSender(services, configuration);
        AddEventConsumers(services, configuration);

        return services;
    }

    private static void AddNotificationOptions(IServiceCollection services, IConfiguration configuration)
    {
        // Тип объявлен в Application (им пользуется NotificationsService), а заполняется здесь:
        // про IConfiguration знает только Infrastructure.
        var section = configuration.GetSection(NotificationOptions.SectionName);
        services.AddSingleton(new NotificationOptions
        {
            StaffEmail = section["StaffEmail"] ?? string.Empty,
        });
    }

    /// <summary>
    /// По консьюмеру на событие из таблицы подписок (spec.md §4) плюс <c>UserRegistered</c> —
    /// он наполняет read-модель контактов, без которой отправлять некуда.
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

        services.AddEventConsumer<UserRegistered>("identity-service");

        services.AddEventConsumer<OrderCreated>("orders-service");
        services.AddEventConsumer<OrderConfirmed>("orders-service");
        services.AddEventConsumer<OrderCancelled>("orders-service");

        services.AddEventConsumer<CargoAccepted>("cargo-service");
        services.AddEventConsumer<CargoStatusChanged>("cargo-service");
        services.AddEventConsumer<CargoDelivered>("cargo-service");

        services.AddEventConsumer<PaymentCompleted>("payment-service");
        services.AddEventConsumer<PaymentFailed>("payment-service");

        services.AddEventConsumer<DocumentGenerated>("document-service");

        services.AddEventConsumer<PackageIntegrityAssessed>("ai-inspection-service");
    }

    /// <summary>
    /// Сервис-издатель задаётся строкой, потому что он часть routing key чужого события, а не
    /// свойство типа: тот же контракт может публиковать другой сервис, и знать об этом должен
    /// подписчик. Значения сверены с таблицей событий spec.md §4 — payment-service,
    /// document-service и ai-inspection-service ещё не построены, их очереди просто останутся
    /// пустыми до Фаз 7–9.
    /// </summary>
    private static void AddEventConsumer<TEvent>(this IServiceCollection services, string publishingService)
        where TEvent : IntegrationEvent =>
        services.AddHostedService(provider => new EventConsumer<TEvent>(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<RabbitMqOptions>(),
            publishingService,
            provider.GetRequiredService<ILogger<EventConsumer<TEvent>>>()));

    private static void AddEmailSender(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(SmtpOptions.SectionName);
        var options = new SmtpOptions
        {
            Host = section["Host"] ?? throw new InvalidOperationException("Smtp:Host is not configured."),
            Port = int.Parse(section["Port"] ?? throw new InvalidOperationException("Smtp:Port is not configured.")),
            UseStartTls = bool.Parse(section["UseStartTls"] ?? throw new InvalidOperationException("Smtp:UseStartTls is not configured.")),
            UserName = section["UserName"],
            Password = section["Password"],
            FromAddress = section["FromAddress"] ?? throw new InvalidOperationException("Smtp:FromAddress is not configured."),
            FromName = section["FromName"] ?? throw new InvalidOperationException("Smtp:FromName is not configured."),
        };

        services.AddSingleton(options);
        services.AddScoped<INotificationSender, SmtpEmailSender>();
    }

    private static void AddSmsSender(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(TwilioSmsOptions.SectionName);
        var options = new TwilioSmsOptions
        {
            BaseUrl = section["BaseUrl"] ?? throw new InvalidOperationException("Sms:BaseUrl is not configured."),
            AccountSid = section["AccountSid"],
            AuthToken = section["AuthToken"],
            FromNumber = section["FromNumber"],
        };

        services.AddSingleton(options);

        var isProviderConfigured = !string.IsNullOrWhiteSpace(options.AccountSid)
            && !string.IsNullOrWhiteSpace(options.AuthToken)
            && !string.IsNullOrWhiteSpace(options.FromNumber);

        if (!isProviderConfigured)
        {
            services.AddScoped<INotificationSender, LoggingSmsSender>();
            return;
        }

        services.AddHttpClient<INotificationSender, TwilioSmsSender>(client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
                // Как у клиентов в orders-service/cargo-service: общий таймаут — предохранитель,
                // отдельную попытку ограничивает политика ниже, иначе он обрежет retry на середине.
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
        Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(10), TimeoutStrategy.Optimistic);
}
