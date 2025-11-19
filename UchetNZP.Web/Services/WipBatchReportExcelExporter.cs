using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using UchetNZP.Web.Models;

namespace UchetNZP.Web.Services;

public interface IWipBatchReportExcelExporter
{
    byte[] Export(WipBatchReportFilterViewModel filter, IReadOnlyList<WipBatchReportItemViewModel> items, decimal totalQuantity);
}

public class WipBatchReportExcelExporter : IWipBatchReportExcelExporter
{
    public byte[] Export(WipBatchReportFilterViewModel filter, IReadOnlyList<WipBatchReportItemViewModel> items, decimal totalQuantity)
    {
        if (filter is null)
        {
            throw new ArgumentNullException(nameof(filter));
        }

        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Остатки партий");

        ConfigurePage(worksheet);

        var rowIndex = 1;
        worksheet.Cell(rowIndex, 1).Value = "Отчёт по остаткам партий НЗП";
        worksheet.Row(rowIndex).Style.Font.SetBold(true);
        worksheet.Row(rowIndex).Style.Font.FontSize = 14;
        worksheet.Row(rowIndex).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Range(rowIndex, 1, rowIndex, 7).Merge();
        rowIndex += 2;

        rowIndex = WriteFilterRow(worksheet, rowIndex, "Период", $"{filter.From:dd.MM.yyyy} — {filter.To:dd.MM.yyyy}");

        if (!string.IsNullOrWhiteSpace(filter.Part))
        {
            rowIndex = WriteFilterRow(worksheet, rowIndex, "Деталь", filter.Part!);
        }

        if (!string.IsNullOrWhiteSpace(filter.Section))
        {
            rowIndex = WriteFilterRow(worksheet, rowIndex, "Вид работ", filter.Section!);
        }

        if (!string.IsNullOrWhiteSpace(filter.OpNumber))
        {
            rowIndex = WriteFilterRow(worksheet, rowIndex, "№ операции", filter.OpNumber!);
        }

        if (rowIndex > 3)
        {
            rowIndex++;
        }

        worksheet.Cell(rowIndex, 1).Value = "Деталь";
        worksheet.Cell(rowIndex, 2).Value = "Обозначение";
        worksheet.Cell(rowIndex, 3).Value = "Вид работ";
        worksheet.Cell(rowIndex, 4).Value = "Операция";
        worksheet.Cell(rowIndex, 5).Value = "Ярлыки";
        worksheet.Cell(rowIndex, 6).Value = "Остаток";
        worksheet.Cell(rowIndex, 7).Value = "Дата партии";
        worksheet.Row(rowIndex).Style.Font.SetBold(true);
        worksheet.Row(rowIndex).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8f9fa");
        worksheet.Row(rowIndex).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        rowIndex++;

        if (items.Count == 0)
        {
            worksheet.Cell(rowIndex, 1).Value = "По выбранным условиям данные не найдены.";
            worksheet.Range(rowIndex, 1, rowIndex, 7).Merge();
            worksheet.Row(rowIndex).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            worksheet.Row(rowIndex).Style.Font.SetItalic(true);
        }
        else
        {
            foreach (var item in items)
            {
                worksheet.Cell(rowIndex, 1).Value = item.PartName;
                worksheet.Cell(rowIndex, 2).Value = item.PartCode ?? string.Empty;
                worksheet.Cell(rowIndex, 3).Value = item.SectionName;
                worksheet.Cell(rowIndex, 4).Value = item.OpNumber;
                worksheet.Cell(rowIndex, 5).Value = item.LabelNumbers ?? string.Empty;
                worksheet.Cell(rowIndex, 6).Value = item.Quantity;
                worksheet.Cell(rowIndex, 6).Style.NumberFormat.Format = "0.###";
                worksheet.Cell(rowIndex, 7).Value = item.BatchDate;
                worksheet.Cell(rowIndex, 7).Style.DateFormat.Format = "dd.MM.yyyy";
                rowIndex++;
            }

            worksheet.Cell(rowIndex, 1).Value = "Итого";
            worksheet.Range(rowIndex, 1, rowIndex, 5).Merge();
            worksheet.Row(rowIndex).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            worksheet.Row(rowIndex).Style.Font.SetBold(true);
            worksheet.Cell(rowIndex, 6).Value = totalQuantity;
            worksheet.Cell(rowIndex, 6).Style.NumberFormat.Format = "0.###";
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static int WriteFilterRow(IXLWorksheet worksheet, int rowIndex, string label, string value)
    {
        worksheet.Cell(rowIndex, 1).Value = label;
        worksheet.Cell(rowIndex, 1).Style.Font.SetBold(true);
        worksheet.Cell(rowIndex, 2).Value = value;
        return rowIndex + 1;
    }

    private static void ConfigurePage(IXLWorksheet worksheet)
    {
        var pageSetup = worksheet.PageSetup;
        pageSetup.PaperSize = XLPaperSize.A4Paper;
        pageSetup.Margins.Top = 0.6;
        pageSetup.Margins.Bottom = 0.6;
        pageSetup.Margins.Left = 0.5;
        pageSetup.Margins.Right = 0.5;
        pageSetup.PageOrientation = XLPageOrientation.Portrait;
        pageSetup.CenterHorizontally = true;
        pageSetup.SetRowsToRepeatAtTop(1, 1);
    }
}
