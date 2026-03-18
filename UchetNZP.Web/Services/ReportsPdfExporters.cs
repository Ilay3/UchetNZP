using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UchetNZP.Shared;
using UchetNZP.Web.Models;

namespace UchetNZP.Web.Services;

public interface IScrapReportPdfExporter
{
    byte[] Export(ScrapReportFilterViewModel filter, IReadOnlyList<ScrapReportItemViewModel> items);
}

public interface ITransferPeriodReportPdfExporter
{
    byte[] Export(TransferPeriodReportFilterViewModel filter, IReadOnlyList<DateTime> dates, IReadOnlyList<TransferPeriodReportItemViewModel> items);
}

public interface IWipBatchReportPdfExporter
{
    byte[] Export(WipBatchReportFilterViewModel filter, IReadOnlyList<WipBatchReportItemViewModel> items, decimal totalQuantity);
}

public class ScrapReportPdfExporter : IScrapReportPdfExporter
{
    public byte[] Export(ScrapReportFilterViewModel filter, IReadOnlyList<ScrapReportItemViewModel> items)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(items);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(10));
                page.Content().Column(column =>
                {
                    column.Item().Text("Отчёт по браку").FontSize(16).Bold();
                    column.Item().Text($"Период: {filter.From:dd.MM.yyyy} — {filter.To:dd.MM.yyyy}");
                    column.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(2);
                        });

                        AddHeaderCell(table, "Дата");
                        AddHeaderCell(table, "Деталь");
                        AddHeaderCell(table, "Операция");
                        AddHeaderCell(table, "Ярлык");
                        AddHeaderCell(table, "Количество");
                        AddHeaderCell(table, "Тип");
                        AddHeaderCell(table, "Комментарий");

                        foreach (var item in items)
                        {
                            AddDataCell(table, item.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
                            AddDataCell(table, item.PartName);
                            AddDataCell(table, item.OpNumber);
                            AddDataCell(table, item.LabelNumber ?? "—");
                            AddDataCell(table, item.Quantity.ToString("0.###", CultureInfo.InvariantCulture));
                            AddDataCell(table, item.ScrapType);
                            AddDataCell(table, item.Comment ?? "—");
                        }

                        if (items.Count == 0)
                        {
                            table.Cell().ColumnSpan(7).Border(1).Padding(4).Text("Данные не найдены").Italic();
                        }
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void AddHeaderCell(TableDescriptor table, string text)
    {
        table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text(text).Bold();
    }

    private static void AddDataCell(TableDescriptor table, string text)
    {
        table.Cell().Border(1).Padding(4).Text(text);
    }
}

public class TransferPeriodReportPdfExporter : ITransferPeriodReportPdfExporter
{
    public byte[] Export(TransferPeriodReportFilterViewModel filter, IReadOnlyList<DateTime> dates, IReadOnlyList<TransferPeriodReportItemViewModel> items)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(dates);
        ArgumentNullException.ThrowIfNull(items);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(9));
                page.Content().Column(column =>
                {
                    column.Item().Text("Отчёт по передаче НЗП за период").FontSize(16).Bold();
                    column.Item().Text($"Период: {filter.From:dd.MM.yyyy} — {filter.To:dd.MM.yyyy}");

                    foreach (var item in items)
                    {
                        column.Item().PaddingTop(8).Border(1).Padding(6).Column(partColumn =>
                        {
                            partColumn.Item().Text(NameWithCodeFormatter.getNameWithCode(item.PartName, item.PartCode)).Bold();
                            foreach (var date in dates)
                            {
                                if (!item.Cells.TryGetValue(date, out var cells) || cells.Count == 0)
                                {
                                    continue;
                                }

                                var values = string.Join("; ", cells);
                                partColumn.Item().Text($"{date:dd.MM.yyyy}: {values}");
                            }
                        });
                    }

                    if (items.Count == 0)
                    {
                        column.Item().PaddingTop(8).Text("Данные не найдены").Italic();
                    }
                });
            });
        });

        return document.GeneratePdf();
    }
}

public class WipBatchReportPdfExporter : IWipBatchReportPdfExporter
{
    public byte[] Export(WipBatchReportFilterViewModel filter, IReadOnlyList<WipBatchReportItemViewModel> items, decimal totalQuantity)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(items);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(10));
                page.Content().Column(column =>
                {
                    column.Item().Text("Отчёт по остаткам партий НЗП").FontSize(16).Bold();
                    column.Item().Text($"Период: {filter.From:dd.MM.yyyy} — {filter.To:dd.MM.yyyy}");
                    column.Item().Text($"Суммарный остаток: {totalQuantity:0.###}");

                    column.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(1);
                        });

                        AddHeaderCell(table, "Деталь");
                        AddHeaderCell(table, "Вид работ");
                        AddHeaderCell(table, "Операция");
                        AddHeaderCell(table, "Ярлыки");
                        AddHeaderCell(table, "Остаток");
                        AddHeaderCell(table, "Дата партии");

                        foreach (var item in items)
                        {
                            AddDataCell(table, item.PartName);
                            AddDataCell(table, item.SectionName);
                            AddDataCell(table, item.OpNumber);
                            AddDataCell(table, item.LabelNumbers ?? "—");
                            AddDataCell(table, item.Quantity.ToString("0.###", CultureInfo.InvariantCulture));
                            AddDataCell(table, item.BatchDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
                        }

                        if (items.Count == 0)
                        {
                            table.Cell().ColumnSpan(6).Border(1).Padding(4).Text("Данные не найдены").Italic();
                        }
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void AddHeaderCell(TableDescriptor table, string text)
    {
        table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text(text).Bold();
    }

    private static void AddDataCell(TableDescriptor table, string text)
    {
        table.Cell().Border(1).Padding(4).Text(text);
    }
}
