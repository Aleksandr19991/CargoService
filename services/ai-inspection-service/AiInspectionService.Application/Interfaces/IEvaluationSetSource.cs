using AiInspectionService.Application.Models;

namespace AiInspectionService.Application.Interfaces;

/// <summary>
/// Источник размеченного тестового набора. Сегодня это папка на диске (см. README сервиса),
/// но интерфейс не привязывает к ней: набор так же естественно живёт в объектном хранилище
/// или в отдельном датасет-сервисе.
/// </summary>
public interface IEvaluationSetSource
{
    /// <summary>Пустой список означает, что набор не настроен или в нём нет снимков.</summary>
    Task<IReadOnlyList<LabelledSample>> LoadAsync(CancellationToken cancellationToken);
}
