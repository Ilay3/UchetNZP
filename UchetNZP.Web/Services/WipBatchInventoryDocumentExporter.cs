using System.Globalization;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UchetNZP.Web.Models;

namespace UchetNZP.Web.Services;

public interface IWipBatchInventoryDocumentExporter
{
    byte[] Export(int inventoryNumber, DateTime generatedAt, DateTime composedAt, WipBatchReportViewModel model);
}

public class WipBatchInventoryDocumentExporter : IWipBatchInventoryDocumentExporter
{
    private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

    public byte[] Export(int inventoryNumber, DateTime generatedAt, DateTime composedAt, WipBatchReportViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var inventory = inventoryNumber.ToString("00", CultureInfo.InvariantCulture);
        var rows = model.Items
            .SelectMany((item, idx) => ExpandRows(item, idx + 1))
            .ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.TimesNewRoman));

                page.Content().Column(column =>
                {
                    column.Spacing(4);

                    column.Item().AlignCenter().Text("ООО \"Промавтоматика\"").FontSize(12);
                    column.Item().LineHorizontal(0.8f);
                    column.Item().AlignCenter().Text("организация").FontSize(8);

                    column.Item().AlignRight().Width(180).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                        });

                        table.Cell().AlignRight().PaddingRight(6).Text("номер");
                        table.Cell().Border(0.8f).PaddingVertical(2).PaddingHorizontal(6).AlignRight().Text($"{inventory}/инв");

                        table.Cell().AlignRight().PaddingRight(6).Text("дата");
                        table.Cell().Border(0.8f).PaddingVertical(2).PaddingHorizontal(6).AlignRight().Text(generatedAt.ToString("dd.MM.yyyy", RuCulture));
                    });

                    column.Item().PaddingTop(6).AlignCenter().Text("АКТ ИНВЕНТАРИЗАЦИИ").FontSize(13).Bold();
                    column.Item().AlignCenter().Text("незавершенного производства").FontSize(11).Bold();

                    column.Item().PaddingTop(6).Text(text =>
                    {
                        text.Span("Акт о том, что по состоянию на ");
                        text.Span(ToLongRussianDate(composedAt)).Bold();
                        text.Span(" проведена инвентаризация незавершенного производства.");
                    });
                    column.Item().Text("При инвентаризации установлено следующее:");

                    column.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(28);
                            columns.RelativeColumn(2.3f);
                            columns.ConstantColumn(56);
                            columns.RelativeColumn(1.8f);
                            columns.ConstantColumn(74);
                            columns.ConstantColumn(72);
                        });

                        static IContainer H(IContainer container)
                            => container.Border(0.8f).Padding(2).Background(Colors.White);

                        static IContainer D(IContainer container)
                            => container.Border(0.8f).Padding(2);

                        table.Cell().RowSpan(3).Element(H).AlignCenter().AlignMiddle().Text("N\nп/п");
                        table.Cell().RowSpan(3).Element(H).AlignCenter().AlignMiddle().Text("Наименование детали в производстве");
                        table.Cell().RowSpan(3).Element(H).AlignCenter().AlignMiddle().Text("Фактическое количество НЗП, шт.");
                        table.Cell().RowSpan(2).Element(H).AlignCenter().AlignMiddle().Text("Операция");
                        table.Cell().ColumnSpan(2).Element(H).AlignCenter().AlignMiddle().Text("Подробная информация");
                        table.Cell().ColumnSpan(2).Element(H).AlignCenter().AlignMiddle().Text("нарастающим итогом");
                        table.Cell().Element(H).AlignCenter().AlignMiddle().Text("Номер ярлыка (партии)");
                        table.Cell().Element(H).AlignCenter().AlignMiddle().Text("Фактическое количество на партии");

                        table.Cell().Element(H).AlignCenter().Text("1");
                        table.Cell().Element(H).AlignCenter().Text("2");
                        table.Cell().Element(H).AlignCenter().Text("3");
                        table.Cell().Element(H).AlignCenter().Text("4");
                        table.Cell().Element(H).AlignCenter().Text("5");
                        table.Cell().Element(H).AlignCenter().Text("6");

                        if (rows.Count == 0)
                        {
                            table.Cell().ColumnSpan(6).Element(D).AlignCenter().Text("Нет данных по выбранным условиям");
                        }
                        else
                        {
                            foreach (var row in rows)
                            {
                                table.Cell().Element(D).AlignCenter().Text(row.IndexText);
                                table.Cell().Element(D).Text(row.PartText);
                                table.Cell().Element(D).AlignCenter().Text(row.TotalQty);
                                table.Cell().Element(D).Text(row.OperationText);
                                table.Cell().Element(D).AlignCenter().Text(row.LabelNumber);
                                table.Cell().Element(D).AlignCenter().Text(row.LabelQty);
                            }
                        }
                    });

                    column.Item().PaddingTop(6).Text($"Итого: {model.TotalQuantity:0.###} шт.").Bold();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static IReadOnlyList<InventoryRow> ExpandRows(WipBatchReportItemViewModel item, int index)
    {
        var labels = ParseLabelRows(item.LabelNumbers, item.Quantity);
        var rows = new List<InventoryRow>(labels.Count);
        for (var i = 0; i < labels.Count; i++)
        {
            var label = labels[i];
            rows.Add(new InventoryRow(
                i == 0 ? index.ToString(CultureInfo.InvariantCulture) : string.Empty,
                i == 0 ? BuildPartText(item) : string.Empty,
                i == 0 ? item.Quantity.ToString("0.###", CultureInfo.InvariantCulture) : string.Empty,
                i == 0 ? $"{item.OpNumber} {item.SectionName}" : string.Empty,
                label.LabelNumber,
                label.Quantity.ToString("0.###", CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static string BuildPartText(WipBatchReportItemViewModel item)
    {
        if (string.IsNullOrWhiteSpace(item.PartCode))
        {
            return item.PartName;
        }

        return $"{item.PartName}\n{item.PartCode}";
    }

    private static string ToLongRussianDate(DateTime date)
        => $"{date.Day} {date.ToString("MMMM", RuCulture)} {date.Year} г.";

    private static IReadOnlyList<(string LabelNumber, decimal Quantity)> ParseLabelRows(string? labels, decimal fallbackQuantity)
    {
        if (string.IsNullOrWhiteSpace(labels))
        {
            return new[] { ("—", fallbackQuantity) };
        }

        var items = labels
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .ToList();

        if (items.Count == 0)
        {
            return new[] { ("—", fallbackQuantity) };
        }

        var result = new List<(string LabelNumber, decimal Quantity)>(items.Count);
        foreach (var raw in items)
        {
            var sep = raw.LastIndexOf(':');
            if (sep <= 0 || sep == raw.Length - 1)
            {
                result.Add((raw, fallbackQuantity));
                continue;
            }

            var label = raw[..sep].Trim();
            var qtyText = raw[(sep + 1)..].Trim();
            if (decimal.TryParse(qtyText, NumberStyles.Number, CultureInfo.InvariantCulture, out var qty)
                || decimal.TryParse(qtyText, NumberStyles.Number, RuCulture, out qty))
            {
                result.Add((string.IsNullOrWhiteSpace(label) ? "—" : label, qty));
            }
            else
            {
                result.Add((string.IsNullOrWhiteSpace(label) ? raw : label, fallbackQuantity));
            }
        }

        return result.Count == 0 ? new[] { ("—", fallbackQuantity) } : result;
    }

    private sealed record InventoryRow(
        string IndexText,
        string PartText,
        string TotalQty,
        string OperationText,
        string LabelNumber,
        string LabelQty);
}
