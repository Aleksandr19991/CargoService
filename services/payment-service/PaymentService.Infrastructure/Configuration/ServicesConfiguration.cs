using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentService.Application.Interfaces;
using PaymentService.Infrastructure.Payments;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace PaymentService.Infrastructure.Configuration;

public static class ServicesConfiguration
{
    // Дальше сюда добавятся консьюмеры `OrderConfirmed`/`OrderCancelled` и outbox для
    // `PaymentCompleted`/`PaymentFailed`/`RefundIssued` — следующие задачи Фазы 9 (spec.md).
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddPaymentProvider(services, configuration);

        return services;
    }

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
