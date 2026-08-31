using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;
using PaymentService.Persistence;
using Testcontainers.PostgreSql;

namespace PaymentService.IntegrationTests;

/// <summary>
/// Поднимает настоящий API поверх одноразового контейнера Postgres (Testcontainers). RabbitMQ и
/// платёжный провайдер не нужны: все <see cref="IHostedService"/> (консьюмеры событий и
/// диспетчер outbox) сняты, а клиент провайдера заменён управляемой заглушкой — проверяется
/// приём webhook, а не чужой HTTP.
/// <para>
/// Учётные данные магазина в настройках заданы намеренно: именно они включают перепроверку
/// статуса у провайдера, то есть боевое поведение, а не поблажку песочницы.
/// </para>
/// </summary>
public class PaymentApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("paymentservice")
        .WithUsername("paymentservice")
        .WithPassword("paymentservice")
        .Build();

    public FakePaymentProviderClient Provider { get; } = new();

    public string ConnectionString => postgres.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting, а не ConfigureAppConfiguration: Program.cs читает строку подключения и
        // секции PaymentProvider/RabbitMQ из builder.Configuration ДО builder.Build(), а
        // источники из ConfigureAppConfiguration подмешиваются только при построении хоста —
        // слишком поздно, и тест молча ушёл бы на адрес из appsettings.json.
        var overrides = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = postgres.GetConnectionString(),
            ["PaymentProvider:BaseUrl"] = "http://provider.invalid/v3",
            ["PaymentProvider:ShopId"] = "test-shop",
            ["PaymentProvider:SecretKey"] = "test-secret",
            ["PaymentProvider:Currency"] = "RUB",
            ["PaymentProvider:ReturnUrl"] = "http://localhost/return",
            ["RabbitMQ:HostName"] = "rabbitmq.invalid",
            ["RabbitMQ:Port"] = "5672",
            ["RabbitMQ:UserName"] = "test",
            ["RabbitMQ:Password"] = "test",
        };

        foreach (var (key, value) in overrides)
            builder.UseSetting(key, value);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();

            services.RemoveAll<IPaymentProviderClient>();
            services.AddSingleton<IPaymentProviderClient>(Provider);
        });
    }

    /// <summary>Кладёт счёт с платежом прямо в БД — консьюмер `OrderConfirmed` в тестах снят.</summary>
    public async Task<(Invoice Invoice, Payment Payment)> SeedInvoiceAsync(
        string providerPaymentId,
        InvoiceStatus invoiceStatus = InvoiceStatus.Issued,
        PaymentStatus paymentStatus = PaymentStatus.Pending,
        decimal amount = 1250.50m)
    {
        var now = DateTimeOffset.UtcNow;

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            Amount = amount,
            Status = paymentStatus,
            ProviderPaymentId = providerPaymentId,
            CreatedAt = now,
        };

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            OrderNumber = $"20260830-{Random.Shared.Next(100000, 999999)}",
            Amount = amount,
            Currency = "RUB",
            Status = invoiceStatus,
            CreatedAt = now,
            PaidAt = invoiceStatus == InvoiceStatus.Paid ? now : null,
            Payments = [payment],
        };

        await using var context = CreateDbContext();
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();

        return (invoice, payment);
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
        // его лениво — без этой строки первый же засев данных падал бы на «relation does not exist».
        using var _ = CreateClient();
    }

    public new async Task DisposeAsync()
    {
        await postgres.StopAsync();
        await base.DisposeAsync();
    }
}
