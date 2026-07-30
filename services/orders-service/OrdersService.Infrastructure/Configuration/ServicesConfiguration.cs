using OrdersService.Application.Interfaces;
using OrdersService.Infrastructure.Messaging;
using OrdersService.Infrastructure.Outbox;
using OrdersService.Infrastructure.Pricing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace OrdersService.Infrastructure.Configuration;

public static class ServicesConfiguration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var pricingSection = configuration.GetSection(PricingClientOptions.SectionName);
        var pricingOptions = new PricingClientOptions
        {
            BaseUrl = pricingSection["BaseUrl"] ?? throw new InvalidOperationException("PricingService:BaseUrl is not configured."),
        };

        services.AddMemoryCache();
        services.AddSingleton<ICalculationCache, InMemoryCalculationCache>();

        services.AddHttpClient<IPricingClient, PricingClient>(client =>
            {
                client.BaseAddress = new Uri(pricingOptions.BaseUrl);
                // Generous overall safety net only — the per-attempt timeout policy below is what
                // actually bounds each try. A tight client.Timeout here would otherwise cut the
                // whole retry+backoff sequence short before it can finish (confirmed by testing
                // against an unreachable pricing-service: 3 retries with exponential backoff need
                // up to ~14s of waiting alone, more than a naive 10s client.Timeout allows).
                client.Timeout = TimeSpan.FromSeconds(60);
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy())
            .AddPolicyHandler(GetTimeoutPolicy());

        var rabbitMqSection = configuration.GetSection(RabbitMqOptions.SectionName);
        var rabbitMqOptions = new RabbitMqOptions
        {
            HostName = rabbitMqSection["HostName"] ?? throw new InvalidOperationException("RabbitMQ:HostName is not configured."),
            Port = int.Parse(rabbitMqSection["Port"] ?? throw new InvalidOperationException("RabbitMQ:Port is not configured.")),
            UserName = rabbitMqSection["UserName"] ?? throw new InvalidOperationException("RabbitMQ:UserName is not configured."),
            Password = rabbitMqSection["Password"] ?? throw new InvalidOperationException("RabbitMQ:Password is not configured."),
        };

        services.AddSingleton(rabbitMqOptions);
        services.AddHostedService<TariffChangedConsumer>();
        services.AddHostedService<CargoStatusChangedConsumer>();
        services.AddHostedService<PaymentCompletedConsumer>();
        services.AddHostedService<OutboxDispatcher>();

        return services;
    }

    // Retries transient failures (5xx, 408, network errors, and the per-attempt timeout below)
    // with exponential backoff — pricing-service being briefly unreachable/slow shouldn't fail
    // order creation outright. Wraps the circuit breaker so each retry attempt is itself observed
    // by (and can trip) the circuit.
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

    // Stops hammering pricing-service once it's clearly down — breaks after 5 consecutive
    // failed attempts, stays open for 30s before letting a probe request through.
    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

    // Bounds each individual attempt (not the whole retry sequence) — without this, a hung
    // connection to pricing-service could block a single attempt indefinitely.
    private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy() =>
        Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(5), TimeoutStrategy.Optimistic);
}
