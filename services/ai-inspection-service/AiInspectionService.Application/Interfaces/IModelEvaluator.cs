using AiInspectionService.Application.Models;

namespace AiInspectionService.Application.Interfaces;

public interface IModelEvaluator
{
    /// <summary>
    /// Прогоняет размеченный набор через модель и считает матрицу ошибок для набора порогов.
    /// <c>null</c>, если набор не настроен или пуст — считать качество не по чему, и это не
    /// ошибка сервера, а отсутствие данных.
    /// </summary>
    Task<ModelEvaluationReport?> EvaluateAsync(CancellationToken cancellationToken);
}
