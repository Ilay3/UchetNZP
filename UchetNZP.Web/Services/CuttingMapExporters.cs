using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UchetNZP.Web.Models;

namespace UchetNZP.Web.Services;

public interface ICuttingMapExcelExporter
{
    byte[] Export(CuttingMapCardViewModel map);
}

public interface ICuttingMapPdfExporter
{
    byte[] Export(CuttingMapCardViewModel map);
}

public class CuttingMapExcelExporter : ICuttingMapExcelExporter
{
    public byte[] Export(CuttingMapCardViewModel map)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Карта раскроя");

        sheet.Cell(1, 1).Value = "Карта раскроя";
        sheet.Row(1).Style.Font.SetBold(true);
        sheet.Row(1).Style.Font.FontSize = 14;
        sheet.Cell(2, 1).Value = $"Требование: {map.RequirementNumber}";
        sheet.Cell(3, 1).Value = $"Деталь: {map.PartDisplay}";
        sheet.Cell(4, 1).Value = $"Тип: {map.Kind}, версия {map.Version}";
        sheet.Cell(5, 1).Value = $"Статус выполнения: {map.ExecutionStatus}";
        sheet.Cell(6, 1).Value = $"Факт. остаток: {(map.ActualResidual.HasValue ? map.ActualResidual.Value.ToString("0.###", CultureInfo.InvariantCulture) : "—")}";

        var row = 8;
        sheet.Cell(row, 1).Value = "Заготовка";
        sheet.Cell(row, 2).Value = "Пошаговый раскрой";
        sheet.Cell(row, 3).Value = "Тип";
        sheet.Cell(row, 4).Value = "Размер";
        sheet.Cell(row, 5).Value = "Координаты";
        sheet.Cell(row, 6).Value = "Поворот";
        sheet.Row(row).Style.Font.SetBold(true);
        row++;

        foreach (var stock in map.Stocks)
        {
            var first = true;
            foreach (var p in stock.Placements)
            {
                sheet.Cell(row, 1).Value = first ? $"#{stock.StockIndex + 1}" : string.Empty;
                sheet.Cell(row, 2).Value = first ? stock.StepDescription : string.Empty;
                sheet.Cell(row, 3).Value = p.ItemType;
                sheet.Cell(row, 4).Value = p.Length.HasValue
                    ? $"{p.Length:0.###}"
                    : $"{p.Width:0.###} x {p.Height:0.###}";
                sheet.Cell(row, 5).Value = p.PositionX.HasValue || p.PositionY.HasValue
                    ? $"X={p.PositionX:0.###}; Y={p.PositionY:0.###}"
                    : "—";
                sheet.Cell(row, 6).Value = p.Rotated ? "Да" : "Нет";
                row++;
                first = false;
            }
        }

        sheet.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}

public class CuttingMapPdfExporter : ICuttingMapPdfExporter
{
    public byte[] Export(CuttingMapCardViewModel map)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.Content().Column(column =>
                {
                    column.Item().Text($"Карта раскроя {map.RequirementNumber}").FontSize(16).Bold();
                    column.Item().Text($"Деталь: {map.PartDisplay}");
                    column.Item().Text($"Тип: {map.Kind}, версия: {map.Version}");
                    column.Item().Text($"Статус: {map.ExecutionStatus}, факт. остаток: {(map.ActualResidual.HasValue ? map.ActualResidual.Value.ToString("0.###", CultureInfo.InvariantCulture) : "—")}");

                    foreach (var stock in map.Stocks)
                    {
                        column.Item().PaddingTop(6).Text($"Заготовка #{stock.StockIndex + 1}").Bold();
                        column.Item().Text(stock.StepDescription);
                        foreach (var p in stock.Placements)
                        {
                            var size = p.Length.HasValue ? $"L={p.Length:0.###}" : $"W={p.Width:0.###}, H={p.Height:0.###}";
                            var coords = p.PositionX.HasValue || p.PositionY.HasValue ? $"X={p.PositionX:0.###}, Y={p.PositionY:0.###}" : "без координат";
                            column.Item().Text($"• {p.ItemType}: {size}; {coords}; поворот: {(p.Rotated ? "да" : "нет")}").FontSize(9);
                        }
                    }
                });
            });
        });

        return document.GeneratePdf();
    }
}
