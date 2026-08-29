using CargoService.Contracts.Events.V1;
using DocumentService.Application.Interfaces;
using DocumentService.Infrastructure.Messaging;
using DocumentService.Infrastructure.Pdf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuestPDF;
using QuestPDF.Infrastructure;

namespace DocumentService.Infrastructure.Configuration;

public static class ServicesConfiguration
{
    // Здесь появятся клиент file-storage-service и outbox для `DocumentGenerated` — следующая
    // задача Фазы 8.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddTrackingCodes(services, configuration);
        AddPdfRenderer(services);
        AddEventConsumers(services, configuration);

        return services;
    }

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
