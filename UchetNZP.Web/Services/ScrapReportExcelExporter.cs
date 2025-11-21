using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using UchetNZP.Web.Models;

namespace UchetNZP.Web.Services;

public interface IScrapReportExcelExporter
{
    byte[] Export(ScrapReportFilterViewModel filter, IReadOnlyList<ScrapReportItemViewModel> items);
}

public class ScrapReportExcelExporter : IScrapReportExcelExporter
{
    public byte[] Export(ScrapReportFilterViewModel filter, IReadOnlyList<ScrapReportItemViewModel> items)
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
        var worksheet = workbook.AddWorksheet("Отчёт по браку");

        var rowIndex = 1;
        worksheet.Cell(rowIndex, 1).Value = "Отчёт по браку";
        worksheet.Row(rowIndex).Style.Font.SetBold(true);
        worksheet.Row(rowIndex).Style.Font.FontSize = 14;
        rowIndex += 2;

        rowIndex = WriteFilterRow(worksheet, rowIndex, "Период", $"{filter.From:dd.MM.yyyy} — {filter.To:dd.MM.yyyy}");

        if (!string.IsNullOrWhiteSpace(filter.Section))
        {
            rowIndex = WriteFilterRow(worksheet, rowIndex, "Вид работ", filter.Section!);
        }

        if (!string.IsNullOrWhiteSpace(filter.Part))
        {
            rowIndex = WriteFilterRow(worksheet, rowIndex, "Деталь", filter.Part!);
        }

        if (!string.IsNullOrWhiteSpace(filter.ScrapType))
        {
            rowIndex = WriteFilterRow(worksheet, rowIndex, "Тип брака", GetScrapTypeDisplayName(filter.ScrapType!));
        }

        if (!string.IsNullOrWhiteSpace(filter.Employee))
        {
            rowIndex = WriteFilterRow(worksheet, rowIndex, "Сотрудник", filter.Employee!);
        }

        if (rowIndex > 3)
        {
            rowIndex++;
        }

        worksheet.Cell(rowIndex, 1).Value = "Дата";
        worksheet.Cell(rowIndex, 2).Value = "Деталь";
        worksheet.Cell(rowIndex, 3).Value = "Обозначение";
        worksheet.Cell(rowIndex, 4).Value = "Операция";
        worksheet.Cell(rowIndex, 5).Value = "Количество";
        worksheet.Cell(rowIndex, 6).Value = "Тип брака";
        worksheet.Cell(rowIndex, 7).Value = "Ярлык";
        worksheet.Cell(rowIndex, 8).Value = "Комментарий";
        worksheet.Row(rowIndex).Style.Font.SetBold(true);
        rowIndex++;

        if (items.Count == 0)
        {
            worksheet.Cell(rowIndex, 1).Value = "По выбранным условиям данные не найдены.";
            worksheet.Range(rowIndex, 1, rowIndex, 8).Merge();
            worksheet.Row(rowIndex).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            worksheet.Row(rowIndex).Style.Font.SetItalic(true);
        }
        else
        {
            foreach (var item in items)
            {
                worksheet.Cell(rowIndex, 1).Value = item.Date;
                worksheet.Cell(rowIndex, 1).Style.DateFormat.Format = "dd.MM.yyyy HH:mm";
                worksheet.Cell(rowIndex, 2).Value = item.PartName;
                worksheet.Cell(rowIndex, 3).Value = item.PartCode ?? string.Empty;
                worksheet.Cell(rowIndex, 4).Value = BuildOperationCellValue(item);
                worksheet.Cell(rowIndex, 5).Value = item.Quantity;
                worksheet.Cell(rowIndex, 5).Style.NumberFormat.Format = "0.###";
                worksheet.Cell(rowIndex, 6).Value = item.ScrapType;
                worksheet.Cell(rowIndex, 7).Value = item.LabelNumber ?? string.Empty;
                worksheet.Cell(rowIndex, 8).Value = item.Comment ?? string.Empty;
                rowIndex++;
            }
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

    private static string BuildOperationCellValue(ScrapReportItemViewModel in_item)
    {
        var ret = in_item.OpNumber;

        if (!string.IsNullOrWhiteSpace(in_item.OperationName))
        {
            ret = string.Concat(in_item.OpNumber, " — ", in_item.OperationName);
        }

        return ret;
    }

    private static string GetScrapTypeDisplayName(string scrapType)
    {
        return scrapType switch
        {
            "Technological" => "Технологический",
            "EmployeeFault" => "По вине сотрудника",
            _ => scrapType,
        };
    }
}
