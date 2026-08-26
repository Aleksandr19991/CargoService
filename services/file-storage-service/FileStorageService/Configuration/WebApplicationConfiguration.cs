using Scalar.AspNetCore;

namespace FileStorageService.API.Configuration;

public static class WebApplicationConfiguration
{
    // Аналога MigrateDatabaseAsync здесь нет: собственной БД у сервиса нет. Создание бакета в
    // хранилище делает BucketInitializer в слое Infrastructure.
    public static WebApplication MapDevelopmentEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        return app;
    }
}
