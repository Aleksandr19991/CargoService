using CargoService.Contracts.Events.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Interfaces;
using PaymentService.Application.Models;
using PaymentService.Infrastructure.Messaging;
using PaymentService.Infrastructure.Outbox;
using PaymentService.Infrastructure.Payments;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace PaymentService.Infrastructure.Configuration;

public static class ServicesConfiguration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddPaymentProvider(services, configuration);
        AddEventConsumers(services, configuration);

        // Публикация PaymentCompleted/PaymentFailed из транзакционного outbox.
        services.AddHostedService<OutboxDispatcher>();

        return services;
    }

    /// <summary>
    /// Подписки сервиса из таблицы событий spec.md §4. <c>OrderCancelled</c> (возвраты)
    /// добавится в задаче 5 Фазы 9.
    /// </summary>
    private static void AddEventConsumers(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(RabbitMqOptions.SectionName);
        services.AddSingleton(new RabbitMqOptions
        {
            HostName = section["HostName"] ?? throw new InvalidOperationException("RabbitMQ:HostName is not configured."),
            Port = int.Parse(section["Port"] ?? throw new InvalidOperationException("RabbitMQ:Port is not configured.")),
            UserName = section["UserName"] ?? throw new InvalidOperationException("RabbitMQ:UserName is not configured."),
            Password = section["Password"] ?? throw new InvalidOperationException("RabbitMQ:Password is not configured."),
        });

        services.AddEventConsumer<OrderConfirmed>("orders-service");
    }

    /// <summary>
    /// Сервис-издатель задаётся строкой, потому что он часть routing key чужого события, а не
    /// свойство типа: тот же контракт может публиковать другой сервис, и знать об этом должен
    /// подписчик.
    /// </summary>
    private static void AddEventConsumer<TEvent>(this IServiceCollection services, string publishingService)
        where TEvent : IntegrationEvent =>
        services.AddHostedService(provider => new EventConsumer<TEvent>(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<RabbitMqOptions>(),
            publishingService,
            provider.GetRequiredService<ILogger<EventConsumer<TEvent>>>()));

    /// <summary>
    /// Боевой клиент подключается, только когда в конфигурации есть учётные данные магазина;
    /// иначе регистрируется песочница (тот же приём, что с SMS-провайдером в
    /// notification-service). Так локальный стек и тесты работают без настоящего эквайринга, а
    /// боевой стенд не может случайно оказаться на заглушке — там ключи заданы.
    /// </summary>
    private static void AddPaymentProvider(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(PaymentProviderOptions.SectionName);
        var options = new PaymentProviderOptions
        {
            BaseUrl = section["BaseUrl"] ?? throw new InvalidOperationException("PaymentProvider:BaseUrl is not configured."),
            ShopId = section["ShopId"],
            SecretKey = section["SecretKey"],
            Currency = section["Currency"] ?? throw new InvalidOperationException("PaymentProvider:Currency is not configured."),
            ReturnUrl = section["ReturnUrl"] ?? throw new InvalidOperationException("PaymentProvider:ReturnUrl is not configured."),
        };

        services.AddSingleton(options);

        var isProviderConfigured = !string.IsNullOrWhiteSpace(options.ShopId)
            && !string.IsNullOrWhiteSpace(options.SecretKey);

        // Валюта нужна сценариям Application (в счёте), но собственного ключа у неё нет — иначе
        // два ключа разъехались бы, и счёт выставлялся бы в одной валюте, а платёж заводился в другой.
        // Перепроверка webhook — тоже не ключ, а следствие: перепроверять статус можно только у
        // подключённого провайдера (см. докблок PaymentOptions).
        services.AddSingleton(new PaymentOptions
        {
            Currency = options.Currency,
            VerifyWebhookWithProvider = isProviderConfigured,
        });

        if (!isProviderConfigured)
        {
            // Singleton, а не Scoped: заглушка помнит заведённые платежи между вызовами, и на
            // каждом запросе новый экземпляр забывал бы их.
            services.AddSingleton<IPaymentProviderClient, SandboxPaymentProviderClient>();
            return;
        }

        services.AddHttpClient<IPaymentProviderClient, YooKassaPaymentProviderClient>(client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
                // Как у клиентов в orders-service/notification-service: общий таймаут — это
                // предохранитель, отдельную попытку ограничивает политика ниже, иначе он обрежет
                // retry-последовательность на середине.
                client.Timeout = TimeSpan.FromSeconds(60);
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy())
            .AddPolicyHandler(GetTimeoutPolicy());
    }

    // Повторы безопасны только потому, что создающие запросы уходят с Idempotence-Key: без него
    // ретрай после таймаута заводил бы у провайдера второй платёж на ту же заявку.
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
