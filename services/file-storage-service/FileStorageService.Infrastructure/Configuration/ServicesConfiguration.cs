using FileStorageService.Application.Interfaces;
using FileStorageService.Application.Models;
using FileStorageService.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;

namespace FileStorageService.Infrastructure.Configuration;

public static class ServicesConfiguration
{
    // Регион фиксируем явно, чтобы SDK не ходил за ним в хранилище при расчёте подписи: клиент
    // для presigning сетевых вызовов делать не должен вовсе.
    private const string Region = "us-east-1";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var options = MinioOptions.Bind(configuration);

        services.AddSingleton(options);

        // Правила загрузки нужны и валидатору запроса в API-слое, поэтому регистрируются
        // отдельным Application-типом: тянуть настройки хранилища в валидатор незачем.
        if (options.AllowedContentTypes.Count == 0)
            throw new InvalidOperationException("Minio:AllowedContentTypes is empty — no file type could ever be uploaded.");

        services.AddSingleton(new FileUploadPolicy
        {
            MaxFileSizeBytes = options.MaxFileSizeBytes,
            AllowedContentTypes = options.AllowedContentTypes,
        });
        services.AddSingleton(_ =>
        {
            var operations = BuildClient(options, options.Endpoint);

            // Если публичный адрес не задан (локальный запуск без docker), подписываем тем же
            // клиентом — снаружи и изнутри хранилище видно одинаково.
            var presigning = string.IsNullOrWhiteSpace(options.PublicEndpoint)
                ? operations
                : BuildClient(options, options.PublicEndpoint);

            return new MinioClients(operations, presigning);
        });

        services.AddSingleton<IFileStorage, MinioFileStorage>();
        services.AddHostedService<BucketInitializer>();

        return services;
    }

    private static IMinioClient BuildClient(MinioOptions options, string endpoint) =>
        new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(options.AccessKey, options.SecretKey)
            .WithRegion(Region)
            .WithSSL(options.UseSsl)
            .Build();
}
