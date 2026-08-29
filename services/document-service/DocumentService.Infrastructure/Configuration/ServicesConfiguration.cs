using DocumentService.Application.Interfaces;
using DocumentService.Infrastructure.Pdf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF;
using QuestPDF.Infrastructure;

namespace DocumentService.Infrastructure.Configuration;

public static class ServicesConfiguration
{
    // Здесь появятся клиент file-storage-service, консьюмеры `CargoAccepted`/`CargoDelivered` и
    // outbox для `DocumentGenerated` — следующие задачи Фазы 8.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddPdfRenderer(services);

        return services;
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
