using AiInspectionService.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiInspectionService.Infrastructure.Inference;

/// <summary>
/// Фоновый рабочий, разбирающий очередь заданий. Отделён от консьюмера событий намеренно: приём
/// события — дело секунд, инференс — дело модели, и связывать их в одну операцию значило бы
/// держать сообщение брокера открытым всё это время.
/// <para>
/// Пока задания есть, они обрабатываются подряд без пауз; на пустой очереди рабочий ждёт
/// <see cref="IdleDelay"/>. Опрос, а не подписка на уведомление БД: пять секунд задержки для
/// проверки фотографий несущественны, а <c>LISTEN/NOTIFY</c> добавил бы вторую, независимую от
/// RabbitMQ, шину со своей обработкой обрывов.
/// </para>
/// </summary>
public class InspectionWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<InspectionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IInspectionProcessor>();

                var processed = await processor.ProcessNextAsync(stoppingToken);
                if (!processed)
                    await Task.Delay(IdleDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down.
            }
            catch (Exception ex)
            {
                // Сюда доходит только то, что не смог обработать сам конвейер: например, БД
                // недоступна и задание нельзя ни взять, ни закрыть. Необработанное исключение в
                // BackgroundService по умолчанию роняет весь хост, поэтому ждём и продолжаем.
                logger.LogError(ex, "Inspection worker iteration failed, retrying in {Delay}", ErrorDelay);
                await Task.Delay(ErrorDelay, stoppingToken);
            }
        }
    }
}
