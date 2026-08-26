using System.Text.RegularExpressions;

namespace NotificationService.Application;

/// <summary>
/// Подстановка значений в плейсхолдеры <c>{{Name}}</c> шаблона.
/// </summary>
public static partial class TemplateRenderer
{
    /// <summary>Чем заменяется плейсхолдер, значение которого у события есть, но пустое.</summary>
    private const string EmptyValuePlaceholder = "—";

    public static string Render(string template, IReadOnlyDictionary<string, string?> values) =>
        PlaceholderPattern().Replace(template, match =>
        {
            var key = match.Groups["key"].Value;

            // Ключа нет вовсе — шаблон просит поле, которого событие не даёт. Плейсхолдер
            // остаётся в тексте как есть: это заметно и в письме, и в истории, тогда как
            // молчаливая замена на пустоту прятала бы рассогласование шаблона с событием.
            if (!values.TryGetValue(key, out var value))
                return match.Value;

            // Ключ есть, но значение пустое (необязательное поле события — местоположение,
            // причина отмены): осмысленного текста нет, и прочерк читается лучше, чем дыра
            // в предложении.
            return string.IsNullOrWhiteSpace(value) ? EmptyValuePlaceholder : value;
        });

    /// <summary>Возвращает плейсхолдеры, которых нет среди значений, — для предупреждения в лог.</summary>
    public static IReadOnlyList<string> FindUnresolved(string template, IReadOnlyDictionary<string, string?> values) =>
        PlaceholderPattern().Matches(template)
            .Select(match => match.Groups["key"].Value)
            .Where(key => !values.ContainsKey(key))
            .Distinct()
            .ToList();

    [GeneratedRegex(@"\{\{(?<key>\w+)\}\}")]
    private static partial Regex PlaceholderPattern();
}
