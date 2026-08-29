namespace AiInspectionService.Domain.Entities;

/// <summary>
/// Вердикт модели по одному снимку задания.
/// <para>
/// В списке полей ТЗ (§2.7) <c>InspectionResult</c> описан без указания снимка, но задание несёт
/// <c>PhotoFileIds[]</c> — несколько фотографий, и одна строка на задание не смогла бы ответить,
/// на каком именно снимке видно повреждение. Поэтому добавлен <see cref="FileId"/>, а сводный
/// вердикт задания (тот, что уходит в событие <c>PackageIntegrityAssessed</c>) собирается из
/// этих строк.
/// </para>
/// </summary>
public class InspectionResult
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }

    /// <summary>Снимок из <see cref="InspectionJob.PhotoFileIds"/>, к которому относится вердикт.</summary>
    public Guid FileId { get; set; }

    /// <summary>Оценка целостности упаковки, 0..1: 1 — «повреждений не видно».</summary>
    public double PackagingIntegrityScore { get; set; }

    public bool DamageDetected { get; set; }

    /// <summary>Уверенность модели в выбранном классе, 0..1.</summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Виды повреждений (вмятина, разрыв, намокание) из ТЗ. Текущая модель — классификатор на
    /// два класса, видов повреждений она не выделяет, поэтому список пока всегда пуст; поле
    /// заведено сразу, чтобы появление детекционной модели не требовало миграции с переносом
    /// уже накопленных вердиктов.
    /// </summary>
    public List<string> DamageTypes { get; set; } = [];

    /// <summary>Версия модели: вердикты разных версий несравнимы (см. метрики качества, задача 8).</summary>
    public string ModelVersion { get; set; } = string.Empty;

    /// <summary>Сырой ответ модели (JSON) — пригодится, когда вердикт оспаривают.</summary>
    public string RawResponse { get; set; } = string.Empty;

    public DateTimeOffset AssessedAt { get; set; }
}
