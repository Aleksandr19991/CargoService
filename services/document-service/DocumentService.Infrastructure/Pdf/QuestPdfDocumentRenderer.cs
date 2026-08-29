using System.Globalization;
using DocumentService.Application.Interfaces;
using DocumentService.Application.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DocumentService.Infrastructure.Pdf;

/// <summary>
/// Вёрстка документов на QuestPDF.
/// <para>
/// Три документа делят общий каркас (шапка перевозчика, заголовок с номером и датой, таблица
/// «поле — значение», подписи, нумерация страниц): бланки различаются набором строк, а не
/// оформлением, и три отдельные вёрстки разъезжались бы при первой же правке шапки.
/// </para>
/// </summary>
public class QuestPdfDocumentRenderer : IDocumentRenderer
{
    private const string CarrierName = "CargoService";
    private const string EmptyValue = "—";

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("ru-RU");

    private readonly ITrackingCodeGenerator trackingCodeGenerator;

    public QuestPdfDocumentRenderer(ITrackingCodeGenerator trackingCodeGenerator)
    {
        this.trackingCodeGenerator = trackingCodeGenerator;
        DocumentFonts.EnsureRegistered();
    }

    public byte[] RenderWaybill(WaybillModel model) =>
        Render(
            "Транспортная накладная",
            model.TrackingNumber,
            model.IssuedAt,
            rows =>
            {
                rows("Номер заявки", model.OrderNumber);
                rows("Отправитель", model.SenderName);
                rows("Получатель", model.RecipientName);
                rows("Город отправления", model.OriginCity);
                rows("Город назначения", model.DestinationCity);
                rows("Вес, кг", Number(model.WeightKg));
                rows("Объём, м³", Number(model.VolumeM3));
                rows("Объявленная ценность, руб.", Money(model.DeclaredValue));
                rows("Стоимость перевозки, руб.", Money(model.Price));
                rows("Срок доставки", Date(model.DeliveryDeadline));
            },
            ["Груз сдал (отправитель)", "Груз принял (перевозчик)"]);

    public byte[] RenderAcceptanceAct(AcceptanceActModel model) =>
        Render(
            "Акт приёма-передачи груза",
            model.TrackingNumber,
            model.AcceptedAt,
            rows =>
            {
                rows("Номер заявки", model.OrderNumber);
                rows("Дата приёмки", DateTime(model.AcceptedAt));
                rows("Состояние упаковки", model.PackagingCondition);
                rows("Состояние груза", model.CargoCondition);
                rows("Фотофиксация, снимков", model.PhotoCount.ToString(Culture));
                rows("Груз принял", model.InspectedByName);
            },
            ["Груз сдал (отправитель)", "Груз принял (склад)"]);

    public byte[] RenderDamageInspectionAct(DamageInspectionActModel model) =>
        Render(
            "Акт осмотра груза при повреждении",
            model.TrackingNumber,
            model.InspectedAt,
            rows =>
            {
                rows("Номер заявки", model.OrderNumber);
                rows("Дата осмотра", DateTime(model.InspectedAt));
                rows("Состояние упаковки", model.PackagingCondition);
                rows("Состояние груза", model.CargoCondition);
                rows("Фотофиксация, снимков", model.PhotoCount.ToString(Culture));
                rows("Обстоятельства", model.Comment);
                rows("Осмотр провёл", model.InspectedByName);

                // Вердикт модели печатается как вспомогательное свидетельство и только если
                // проверка была: строка «автоматическая проверка: —» в акте о повреждении
                // выглядит так, будто её замяли.
                if (model.AiDamageDetected is { } damageDetected)
                {
                    rows(
                        "Автоматическая проверка фото",
                        damageDetected ? "повреждение обнаружено" : "повреждение не обнаружено");
                    rows("Уверенность модели", model.AiConfidence?.ToString("P0", Culture));
                }
            },
            ["Осмотр провёл (склад)", "С актом ознакомлен (клиент)"]);

    private byte[] Render(
        string title,
        string trackingNumber,
        DateTimeOffset issuedAt,
        Action<Action<string, string?>> buildRows,
        IReadOnlyList<string> signatures)
    {
        var rows = new List<(string Label, string Value)>();
        buildRows((label, value) => rows.Add((label, string.IsNullOrWhiteSpace(value) ? EmptyValue : value)));

        // Код печатается на каждом документе и собирается здесь, а не приходит в модели: он
        // выводится из трек-номера, и заставлять вызывающий код помнить о нём значило бы
        // однажды выпустить бланк без кода.
        var trackingCodeImage = trackingCodeGenerator.CreateTrackingCode(trackingNumber);

        var document = QuestPDF.Fluent.Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(text => text.FontFamily(DocumentFonts.FamilyName).FontSize(10));

            page.Header().Element(header => ComposeHeader(header, title, trackingNumber, issuedAt, trackingCodeImage));
            page.Content().PaddingVertical(15).Element(content => ComposeRows(content, rows));
            page.Footer().Element(footer => ComposeFooter(footer, signatures));
        }));

        return document.GeneratePdf();
    }

    private static void ComposeHeader(
        IContainer container,
        string title,
        string trackingNumber,
        DateTimeOffset issuedAt,
        byte[] trackingCodeImage)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(CarrierName).FontSize(16).SemiBold();
                    left.Item().Text("Грузоперевозки").FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                // ~2 см в шапке: код должен читаться телефоном с распечатанного бланка, но не
                // спорить с ним за место.
                row.ConstantItem(60).Image(trackingCodeImage);
            });

            column.Item().PaddingTop(10).Text(title).FontSize(14).SemiBold();
            column.Item().PaddingTop(2).Text($"Трек-номер {trackingNumber} · от {DateTime(issuedAt)}")
                .FontSize(10).FontColor(Colors.Grey.Darken2);
            column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private static void ComposeRows(IContainer container, IReadOnlyList<(string Label, string Value)> rows)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(200);
                columns.RelativeColumn();
            });

            foreach (var (label, value) in rows)
            {
                table.Cell().Element(Cell).Text(label).FontColor(Colors.Grey.Darken2);
                table.Cell().Element(Cell).Text(value);
            }
        });

        return;

        static IContainer Cell(IContainer cell) =>
            cell.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5).PaddingRight(10);
    }

    private static void ComposeFooter(IContainer container, IReadOnlyList<string> signatures)
    {
        container.Column(column =>
        {
            column.Item().PaddingBottom(10).Row(row =>
            {
                foreach (var signature in signatures)
                {
                    row.RelativeItem().Column(cell =>
                    {
                        cell.Item().PaddingTop(20).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                        cell.Item().PaddingTop(3).Text(signature).FontSize(8).FontColor(Colors.Grey.Darken1);
                    });

                    row.ConstantItem(30);
                }
            });

            column.Item().AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Darken1));
                text.Span("Страница ");
                text.CurrentPageNumber();
                text.Span(" из ");
                text.TotalPages();
            });
        });
    }

    private static string? Money(decimal? value) => value?.ToString("N2", Culture);

    private static string? Number(decimal? value) => value?.ToString("0.###", Culture);

    private static string? Date(DateTimeOffset? value) => value?.ToString("dd.MM.yyyy", Culture);

    private static string DateTime(DateTimeOffset value) => value.ToString("dd.MM.yyyy HH:mm", Culture);
}
