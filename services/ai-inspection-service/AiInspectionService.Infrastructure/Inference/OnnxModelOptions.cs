namespace AiInspectionService.Infrastructure.Inference;

/// <summary>
/// Настройки локальной ONNX-модели.
/// <para>
/// Сам файл модели в репозиторий не кладётся: это бинарь на десятки мегабайт, который меняется
/// не вместе с кодом и версионируется отдельно (см. README сервиса). Путь и версия задаются
/// конфигурацией, в docker-compose файл прокидывается томом.
/// </para>
/// </summary>
public sealed class OnnxModelOptions
{
    public const string SectionName = "AiModel";

    /// <summary>
    /// Путь к файлу <c>.onnx</c>. Пусто или файла нет — сервис поднимется на заглушке
    /// (<see cref="StubPackageInspectionModel"/>), а не упадёт: без модели остальной конвейер
    /// (события, задания, API) должен оставаться проверяемым.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Версия модели для вердиктов. Задаётся конфигурацией, а не читается из файла: у ONNX нет
    /// обязательного поля версии, а сравнивать результаты по хэшу файла неудобно.
    /// </summary>
    public string Version { get; init; } = "unknown";

    /// <summary>
    /// Папка с размеченным тестовым набором (<c>damaged/</c> и <c>intact/</c> внутри). Пусто —
    /// оценка качества недоступна; в репозиторий и образ набор не кладётся.
    /// </summary>
    public string? EvaluationSetPath { get; init; }
}
