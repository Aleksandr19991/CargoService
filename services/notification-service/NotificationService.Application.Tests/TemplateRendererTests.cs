using NotificationService.Application;

namespace NotificationService.Application.Tests;

public class TemplateRendererTests
{
    [Fact]
    public void Render_SubstitutesKnownPlaceholders()
    {
        var result = TemplateRenderer.Render(
            "Груз {{TrackingNumber}}: {{Status}}.",
            new Dictionary<string, string?> { ["TrackingNumber"] = "CS-ABC123", ["Status"] = "InTransit" });

        Assert.Equal("Груз CS-ABC123: InTransit.", result);
    }

    [Fact]
    public void Render_ReplacesEmptyValueWithDash()
    {
        // Необязательное поле события (местоположение, причина отмены) — прочерк читается лучше,
        // чем дыра в предложении.
        var result = TemplateRenderer.Render(
            "Причина: {{Reason}}",
            new Dictionary<string, string?> { ["Reason"] = null });

        Assert.Equal("Причина: —", result);
    }

    [Fact]
    public void Render_LeavesUnknownPlaceholderAsIs()
    {
        // Шаблон просит поле, которого событие не даёт: плейсхолдер остаётся видимым и в письме,
        // и в истории — молчаливая замена на пустоту прятала бы рассогласование.
        var result = TemplateRenderer.Render(
            "Заявка {{OrderNumber}} на {{Amount}}",
            new Dictionary<string, string?> { ["OrderNumber"] = "20260827-AB12CD" });

        Assert.Equal("Заявка 20260827-AB12CD на {{Amount}}", result);
    }

    [Fact]
    public void FindUnresolved_ReturnsOnlyPlaceholdersWithoutValues()
    {
        var unresolved = TemplateRenderer.FindUnresolved(
            "{{A}} {{B}} {{A}} {{C}}",
            new Dictionary<string, string?> { ["A"] = "a" });

        Assert.Equal(["B", "C"], unresolved);
    }
}
