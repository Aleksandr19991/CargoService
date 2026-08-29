using System.Reflection;
using QuestPDF.Drawing;

namespace DocumentService.Infrastructure.Pdf;

/// <summary>
/// Регистрирует шрифт документов в QuestPDF.
/// <para>
/// Шрифт вшит в сборку, а не берётся из системы: в образе <c>dotnet/aspnet</c> шрифтов нет
/// вовсе, и документ с кириллицей вышел бы из контейнера набором пустых прямоугольников —
/// причём молча, без единой ошибки в логе. Ставить пакет шрифтов в Dockerfile значило бы
/// поставить вид печатного документа в зависимость от базового образа.
/// </para>
/// <para>
/// Взят PT Sans (SIL Open Font License 1.1, лицензия рядом с файлами шрифта): открытая
/// лицензия прямо разрешает вложение и распространение, а сам шрифт делался для русского
/// набора — то есть с полной кириллицей, чего у многих «дефолтных» шрифтов нет.
/// </para>
/// </summary>
public static class DocumentFonts
{
    public const string FamilyName = "PT Sans";

    private static readonly Lock RegistrationLock = new();
    private static bool registered;

    /// <summary>
    /// Идемпотентна: QuestPDF держит шрифты в статическом реестре, и повторная регистрация в
    /// тестах (где хост поднимается по разу на класс) только тратила бы время.
    /// </summary>
    public static void EnsureRegistered()
    {
        if (registered)
            return;

        lock (RegistrationLock)
        {
            if (registered)
                return;

            foreach (var resourceName in new[]
                     {
                         "DocumentService.Infrastructure.Fonts.PT_Sans-Web-Regular.ttf",
                         "DocumentService.Infrastructure.Fonts.PT_Sans-Web-Bold.ttf",
                     })
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                    ?? throw new InvalidOperationException($"Font resource {resourceName} is missing from the assembly.");

                FontManager.RegisterFont(stream);
            }

            registered = true;
        }
    }
}
