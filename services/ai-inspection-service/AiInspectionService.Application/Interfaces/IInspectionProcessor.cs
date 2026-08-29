namespace AiInspectionService.Application.Interfaces;

public interface IInspectionProcessor
{
    /// <summary>
    /// Берёт одно задание из очереди и прогоняет его снимки через модель. Возвращает
    /// <c>false</c>, если очередь пуста, — по этому признаку фоновый рабочий решает, идти ли
    /// за следующим заданием сразу или подождать.
    /// </summary>
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken);
}
