namespace DocumentService.Application.Interfaces;

public interface IDocumentGenerationProcessor
{
    /// <summary>
    /// Берёт один непечатанный документ, собирает PDF, кладёт его в хранилище и публикует
    /// событие. Возвращает <c>false</c>, если печатать нечего, — по этому признаку фоновый
    /// рабочий решает, брать ли следующий сразу или подождать.
    /// </summary>
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken);
}
