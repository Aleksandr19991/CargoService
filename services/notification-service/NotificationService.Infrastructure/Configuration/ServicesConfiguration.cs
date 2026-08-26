using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.Interfaces;
using NotificationService.Infrastructure.Email;
using NotificationService.Infrastructure.Sms;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace NotificationService.Infrastructure.Configuration;

public static class ServicesConfiguration
{
    // Консьюмеры статусных событий (задача 4 Фазы 6) регистрируются здесь же — по образцу
    // консьюмеров cargo-service. Outbox не предполагается: сервис ничего не публикует.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddEmailSender(services, configuration);
        AddSmsSender(services, configuration);

        return services;
    }

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
