using ClosedXML.Excel;
using UchetNZP.Web.Models;

namespace UchetNZP.Web.Services;

public interface IFullMovementReportExcelExporter
{
    byte[] Export(IReadOnlyList<FullMovementReportRowViewModel> rows);
}

public sealed class FullMovementReportExcelExporter : IFullMovementReportExcelExporter
{
    private static readonly string[] Headers =
    [
        "Партия / ярлык",
        "Дата",
        "Деталь",
        "Материал - Артикул",
        "Норма, м/шт",
        "Запуск, шт",
        "Металл выдан, м",
        "№ опер.",
        "Этап",
        "Операция",
        "Вход на этап, шт",
        "Передано дальше, шт",
        "Сдано на склад ГП, шт",
        "Брак выявлен, шт",
        "НЗП после этапа, шт",
        "Потеря металла в браке, м",
        "% потерь этапа",
        "Где возник брак",
        "Причина брака",
        "Документ / акт",
        "Статус",
    ];

    public byte[] Export(IReadOnlyList<FullMovementReportRowViewModel> rows)
    {
        if (rows is null)
        {
            throw new ArgumentNullException(nameof(rows));
        }

        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Отчет");

        ConfigurePage(worksheet);
        WriteHeader(worksheet);
        WriteRows(worksheet, rows);

        worksheet.Columns().AdjustToContents(8, 38);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteHeader(IXLWorksheet worksheet)
    {
        worksheet.Cell(1, 1).Value = "Маршрут партии: от склада металла до склада готовой продукции";
        worksheet.Range(1, 1, 1, Headers.Length).Merge();
        worksheet.Row(1).Style.Font.SetBold(true);
        worksheet.Row(1).Style.Font.FontSize = 14;
        worksheet.Row(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        for (var index = 0; index < Headers.Length; index++)
        {
            var cell = worksheet.Cell(3, index + 1);
            cell.Value = Headers[index];
            cell.Style.Font.SetBold(true);
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#5B9BD5");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Alignment.WrapText = true;
        }

        worksheet.Row(3).Height = 36;
    }

    private static void WriteRows(IXLWorksheet worksheet, IReadOnlyList<FullMovementReportRowViewModel> rows)
    {
        if (rows.Count == 0)
        {
            worksheet.Cell(4, 1).Value = "По выбранным ярлыкам движения не найдены.";
            worksheet.Range(4, 1, 4, Headers.Length).Merge();
            worksheet.Row(4).Style.Font.SetItalic(true);
            return;
        }

        var rowIndex = 4;
        foreach (var item in rows)
        {
            worksheet.Cell(rowIndex, 1).Value = item.LabelNumber;
            worksheet.Cell(rowIndex, 2).Value = item.Date;
            worksheet.Cell(rowIndex, 2).Style.DateFormat.Format = "dd.MM.yyyy";
            worksheet.Cell(rowIndex, 3).Value = FormatPart(item.PartName, item.PartCode);
            worksheet.Cell(rowIndex, 4).Value = item.MaterialDisplay;
            if (item.NormPerUnit.HasValue)
            {
                worksheet.Cell(rowIndex, 5).Value = item.NormPerUnit.Value;
            }
            worksheet.Cell(rowIndex, 6).Value = item.LaunchQuantity;
            worksheet.Cell(rowIndex, 7).FormulaA1 = $"=IF(OR(E{rowIndex}=\"\",F{rowIndex}=\"\"),\"\",E{rowIndex}*F{rowIndex})";
            worksheet.Cell(rowIndex, 8).Value = item.OpNumber;
            worksheet.Cell(rowIndex, 9).Value = item.Stage;
            worksheet.Cell(rowIndex, 10).Value = item.OperationName;
            worksheet.Cell(rowIndex, 11).Value = item.InputQuantity;
            worksheet.Cell(rowIndex, 12).Value = item.TransferredQuantity;
            worksheet.Cell(rowIndex, 13).Value = item.WarehouseQuantity;
            worksheet.Cell(rowIndex, 14).Value = item.ScrapQuantity;
            worksheet.Cell(rowIndex, 15).FormulaA1 = $"=IF($A{rowIndex}=\"\",\"\",K{rowIndex}-L{rowIndex}-M{rowIndex}-N{rowIndex})";
            worksheet.Cell(rowIndex, 16).FormulaA1 = $"=IF($A{rowIndex}=\"\",\"\",N{rowIndex}*E{rowIndex})";
            worksheet.Cell(rowIndex, 17).FormulaA1 = $"=IF($A{rowIndex}=\"\", \"\", IFERROR(N{rowIndex}/K{rowIndex},0))";
            worksheet.Cell(rowIndex, 18).Value = item.ScrapOrigin;
            worksheet.Cell(rowIndex, 19).Value = item.ScrapReason;
            worksheet.Cell(rowIndex, 20).Value = item.DocumentName;
            worksheet.Cell(rowIndex, 21).Value = item.Status;

            worksheet.Range(rowIndex, 5, rowIndex, 7).Style.NumberFormat.Format = "0.###";
            worksheet.Range(rowIndex, 11, rowIndex, 16).Style.NumberFormat.Format = "0.###";
            worksheet.Cell(rowIndex, 17).Style.NumberFormat.Format = "0.00%";
            rowIndex++;
        }

        var dataRange = worksheet.Range(3, 1, rowIndex - 1, Headers.Length);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Range(4, 1, rowIndex - 1, Headers.Length).Style.Alignment.WrapText = true;
    }

    private static void ConfigurePage(IXLWorksheet worksheet)
    {
        worksheet.SheetView.FreezeRows(3);
        worksheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        worksheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        worksheet.PageSetup.Margins.Top = 0.4;
        worksheet.PageSetup.Margins.Bottom = 0.4;
        worksheet.PageSetup.Margins.Left = 0.3;
        worksheet.PageSetup.Margins.Right = 0.3;
        worksheet.PageSetup.SetRowsToRepeatAtTop(1, 3);
    }

    private static string FormatPart(string name, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return name;
        }

        return $"{name} {code}";
    }
}
