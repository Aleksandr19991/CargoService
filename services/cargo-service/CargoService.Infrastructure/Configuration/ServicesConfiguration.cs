using CargoService.Infrastructure.Messaging;
using CargoService.Infrastructure.Outbox;
using CargoService.Infrastructure.Sla;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CargoService.Infrastructure.Configuration;

public static class ServicesConfiguration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
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
}
