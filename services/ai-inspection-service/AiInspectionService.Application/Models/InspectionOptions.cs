namespace AiInspectionService.Application.Models;

/// <summary>
/// Правила, по которым вердикты модели превращаются в вывод сервиса. Отделены от настроек
/// самого файла модели (те живут в Infrastructure): это бизнес-решение, а не свойство ONNX.
/// Заполняется из конфигурации в Infrastructure — Application про <c>IConfiguration</c> не знает.
/// </summary>
public sealed class InspectionOptions
{
    public const string SectionName = "AiModel";

    /// <summary>
    /// Ниже этой уверенности вердикт «повреждено» не считается обнаружением: «повреждено» с
    /// уверенностью 0.51 — догадка, и на складе от неё вреда больше, чем пользы. Значение
    /// выбирается по отчёту <c>POST /api/inspections/evaluate</c> (матрица ошибок по порогам).
    /// <para>
    /// Умолчание 0.7 совпадает с порогом, который cargo-service применяет к флагу расхождения
    /// (Фаза 5). Пороги не дублируют друг друга: здесь решается, называть ли повреждение
    /// обнаруженным, там — поднимать ли тревогу из-за расхождения с оценкой сотрудника.
    /// </para>
    /// </summary>
    public double DamageConfidenceThreshold { get; init; } = 0.7;
}
