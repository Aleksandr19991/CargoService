using AiInspectionService.Application.Interfaces;
using AiInspectionService.Application.Models;
using AiInspectionService.Domain.Entities;
using AiInspectionService.Domain.Enums;
using AiInspectionService.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace AiInspectionService.IntegrationTests;

/// <summary>
/// Поднимает настоящий API поверх одноразового контейнера Postgres (Testcontainers). Ни Keycloak,
/// ни RabbitMQ, ни MinIO, ни файла модели не нужно: <see cref="TestAuthHandler"/> заменяет
/// JwtBearer, все <see cref="IHostedService"/> (consumer, outbox-диспетчер, фоновый рабочий)
/// сняты, а модель и тестовый набор подменены на управляемые тестом заглушки.
/// </summary>
public class InspectionApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("aiinspectionservice")
        .WithUsername("aiinspectionservice")
        .WithPassword("aiinspectionservice")
        .Build();

    /// <summary>Набор, который отдаёт подменённый источник; тест наполняет его под свой случай.</summary>
    public List<LabelledSample> EvaluationSet { get; } = [];

    public string ConnectionString => postgres.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting, а не ConfigureAppConfiguration: Program.cs читает строку подключения и
        // секции Keycloak/RabbitMQ/FileStorage из builder.Configuration ДО builder.Build(), а
        // источники из ConfigureAppConfiguration подмешиваются только при построении хоста —
        // слишком поздно, тест молча ушёл бы на адрес из appsettings.json (см. Фазу 6, задача 7).
        var overrides = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = postgres.GetConnectionString(),
            ["Keycloak:BaseUrl"] = "http://keycloak.invalid",
            ["Keycloak:Realm"] = "cargoservice",
            ["Keycloak:ClientId"] = "identity-service",
            ["Keycloak:ValidIssuer"] = "http://keycloak.invalid/realms/cargoservice",
            ["KeycloakServiceAccount:BaseUrl"] = "http://keycloak.invalid",
            ["KeycloakServiceAccount:Realm"] = "cargoservice",
            ["KeycloakServiceAccount:ClientId"] = "ai-inspection-service",
            ["KeycloakServiceAccount:ClientSecret"] = "test",
            ["FileStorageService:BaseUrl"] = "http://filestorage.invalid",
            ["RabbitMQ:HostName"] = "rabbitmq.invalid",
            ["RabbitMQ:Port"] = "5672",
            ["RabbitMQ:UserName"] = "test",
            ["RabbitMQ:Password"] = "test",
            ["AiModel:Version"] = "test-model",
        };

        foreach (var (key, value) in overrides)
            builder.UseSetting(key, value);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();

            services.RemoveAll<IEvaluationSetSource>();
            services.AddSingleton<IEvaluationSetSource>(new StubEvaluationSetSource(EvaluationSet));

            services.RemoveAll<IPackageInspectionModel>();
            services.AddSingleton<IPackageInspectionModel>(new StubModel());

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    /// <summary>Кладёт готовое задание с вердиктами прямо в БД — фоновый рабочий в тестах снят.</summary>
    public async Task<InspectionJob> SeedJobAsync(InspectionJobStatus status = InspectionJobStatus.Completed)
    {
        var now = DateTimeOffset.UtcNow;
        var job = new InspectionJob
        {
            Id = Guid.NewGuid(),
            ShipmentId = Guid.NewGuid(),
            PhotoFileIds = [Guid.NewGuid()],
            Status = status,
            CreatedAt = now,
            StartedAt = status == InspectionJobStatus.Queued ? null : now,
            CompletedAt = status is InspectionJobStatus.Completed or InspectionJobStatus.Failed ? now : null,
        };

        if (status == InspectionJobStatus.Completed)
        {
            job.Results.Add(new InspectionResult
            {
                FileId = job.PhotoFileIds[0],
                PackagingIntegrityScore = 0.12,
                DamageDetected = true,
                Confidence = 0.88,
                ModelVersion = "test-model",
                RawResponse = """{"scores":[-1.2,0.8]}""",
                AssessedAt = now,
            });
        }

        await using var context = CreateDbContext();
        context.InspectionJobs.Add(job);
        await context.SaveChangesAsync();

        return job;
    }

    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new AppDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();

        // Схему создаёт хост при старте (MigrateDatabaseAsync), а WebApplicationFactory поднимает
        // его лениво — без этой строки тест, засеивающий задание до первого запроса, падает на
        // «relation inspection_jobs does not exist».
        using var _ = CreateClient();
    }

    public new async Task DisposeAsync()
    {
        await postgres.StopAsync();
        await base.DisposeAsync();
    }

    private sealed class StubEvaluationSetSource(List<LabelledSample> samples) : IEvaluationSetSource
    {
        public Task<IReadOnlyList<LabelledSample>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LabelledSample>>(samples);
    }

    /// <summary>Вердикт зависит от первого байта снимка — тесту этого хватает, чтобы собрать нужную матрицу.</summary>
    private sealed class StubModel : IPackageInspectionModel
    {
        public string Version => "test-model";

        public Task<PackageInspectionVerdict> InspectAsync(byte[] imageBytes, CancellationToken cancellationToken)
        {
            var damaged = imageBytes.Length > 0 && imageBytes[0] == (byte)'d';

            return Task.FromResult(new PackageInspectionVerdict
            {
                PackagingIntegrityScore = damaged ? 0.1 : 0.9,
                DamageDetected = damaged,
                Confidence = 0.9,
                ModelVersion = Version,
                RawResponse = "{}",
            });
        }
    }
}
