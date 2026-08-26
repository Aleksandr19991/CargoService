using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.Interfaces;

namespace NotificationService.Application.Configuration;

public static class ServicesConfiguration
{
    public static void AddApplicationServices(this IServiceCollection services)
    {
        // Scoped, а не singleton: среди отправителей есть типизированный HttpClient (SMS), а он
        // регистрируется transient — захват его синглтоном заморозил бы HttpMessageHandler на
        // весь процесс, мимо ротации, которую делает IHttpClientFactory.
        services.AddScoped<INotificationSenderRegistry, NotificationSenderRegistry>();
    }
}
