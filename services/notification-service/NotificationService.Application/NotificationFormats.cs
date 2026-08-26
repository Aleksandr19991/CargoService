using System.Globalization;

namespace NotificationService.Application;

/// <summary>
/// Единое форматирование значений, попадающих в тексты уведомлений. Вынесено из обработчиков,
/// чтобы сумма и дата выглядели одинаково во всех письмах, а не как их написал автор шаблона.
/// Культура задана явно: уведомления читает клиент, а не машина, на которой крутится сервис.
/// </summary>
public static class NotificationFormats
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("ru-RU");

    public static string Money(decimal value) => value.ToString("N2", Culture);

    public static string Date(DateTimeOffset value) => value.ToString("dd.MM.yyyy", Culture);

    public static string? Date(DateTimeOffset? value) => value is null ? null : Date(value.Value);

    public static string Percent(double value) => value.ToString("P0", Culture);
}
