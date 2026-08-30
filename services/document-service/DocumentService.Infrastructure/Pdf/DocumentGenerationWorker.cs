using DocumentService.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocumentService.Infrastructure.Pdf;

/// <summary>
/// Фоновый рабочий, печатающий заведённые документы. Отделён от консьюмеров событий намеренно:
/// приём события — дело секунд, а вёрстка PDF и загрузка файла в хранилище — нет, и связывать
/// их в одну операцию значило бы держать сообщение брокера открытым всё это время.
/// <para>
/// Пока документы есть, они печатаются подряд; на пустой очереди рабочий ждёт
/// <see cref="IdleDelay"/> — тот же приём, что в ai-inspection-service.
/// </para>
/// </summary>
public class DocumentGenerationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<DocumentGenerationWorker> logger) : BackgroundService
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
                var processor = scope.ServiceProvider.GetRequiredService<IDocumentGenerationProcessor>();

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
                // недоступна и документ нельзя ни взять, ни закрыть. Необработанное исключение в
                // BackgroundService по умолчанию роняет весь хост, поэтому ждём и продолжаем.
                logger.LogError(ex, "Document generation iteration failed, retrying in {Delay}", ErrorDelay);
                await Task.Delay(ErrorDelay, stoppingToken);
            }
        }
    }
}
