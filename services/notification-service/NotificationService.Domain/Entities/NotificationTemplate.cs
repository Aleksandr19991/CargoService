using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

/// <summary>
/// Шаблон текста уведомления (spec.md §2.6). Один повод (<see cref="Code"/>) имеет свой шаблон
/// в каждом канале: письмо и SMS про одно и то же событие пишутся по-разному, поэтому пара
/// Code+Channel уникальна, а сам Code — не уникален.
/// </summary>
public class NotificationTemplate
{
    public Guid Id { get; set; }

    /// <summary>
    /// Код повода — совпадает с именем события, по которому уведомление отправляется
    /// (<c>OrderCreated</c>, <c>CargoStatusChanged</c>, …). Значения — в
    /// <c>NotificationService.Application.NotificationTemplateCodes</c>, консьюмеры (Фаза 6,
    /// задача 4) ищут шаблон по паре Code + Channel.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    public NotificationChannel Channel { get; set; }

    /// <summary>Тема письма. Null у SMS и Push — у них темы нет вовсе, а не «пустая».</summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Тело с плейсхолдерами вида <c>{{TrackingNumber}}</c>: их подставляет отправка
    /// уведомления значениями из события. Синтаксис намеренно примитивный (без условий и
    /// циклов) — шаблоны правит человек, а не программист, и всё, что им нужно, это подстановка.
    /// </summary>
    public string Body { get; set; } = string.Empty;
}
