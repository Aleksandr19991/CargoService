using CargoService.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CargoService.Infrastructure.Sla;

/// <summary>
/// Периодически переводит грузы с истёкшим сроком доставки в статус «Задерживается»
/// (spec.md Фаза 5). Смена статуса идёт обычным путём — с записью в историю и публикацией
/// CargoStatusChanged через outbox, — поэтому клиент видит задержку и в трекинге, и в заявке.
/// </summary>
public class SlaMonitor(
    IServiceScopeFactory scopeFactory,
    ILogger<SlaMonitor> logger) : BackgroundService
{
    // Срок доставки измеряется днями, так что чаще пяти минут проверять незачем: раньше об
    // истечении узнать всё равно нельзя, а лишние проходы — это лишние запросы к БД.
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    // За проход помечаем ограниченную пачку: если просроченных накопилось много (сервис долго
    // лежал), они разойдутся за несколько проходов, а не одной гигантской транзакцией.
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var flagged = await FlagOverdueAsync(stoppingToken);

                // Пачка заполнилась — значит, просроченных больше, чем влезло: продолжаем сразу,
                // не выжидая интервал.
                var delay = flagged == BatchSize ? TimeSpan.Zero : CheckInterval;
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down.
            }
            catch (Exception ex)
            {
                // БД недоступна или запрос упал — не роняем хост (необработанное исключение в
                // BackgroundService по умолчанию гасит всё приложение), пробуем снова позже.
                logger.LogError(ex, "SLA monitor pass failed, retrying in {Delay}", RetryDelay);
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }

    private async Task<int> FlagOverdueAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var shipmentsService = scope.ServiceProvider.GetRequiredService<IShipmentsService>();

        var flagged = await shipmentsService.FlagOverdueShipmentsAsync(BatchSize, cancellationToken);
        if (flagged > 0)
            logger.LogInformation("SLA monitor flagged {Count} overdue shipment(s) as Delayed", flagged);

        return flagged;
    }
}
