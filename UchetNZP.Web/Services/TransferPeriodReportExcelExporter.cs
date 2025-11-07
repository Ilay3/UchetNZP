using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using UchetNZP.Web.Models;

namespace UchetNZP.Web.Services;

public interface ITransferPeriodReportExcelExporter
{
    byte[] Export(
        TransferPeriodReportFilterViewModel in_filter,
        IReadOnlyList<DateTime> in_dates,
        IReadOnlyList<TransferPeriodReportItemViewModel> in_items);
}

public class TransferPeriodReportExcelExporter : ITransferPeriodReportExcelExporter
{
    public byte[] Export(
        TransferPeriodReportFilterViewModel in_filter,
        IReadOnlyList<DateTime> in_dates,
        IReadOnlyList<TransferPeriodReportItemViewModel> in_items)
    {
        if (in_filter is null)
        {
            throw new ArgumentNullException(nameof(in_filter));
        }

        if (in_dates is null)
        {
            throw new ArgumentNullException(nameof(in_dates));
        }

        if (in_items is null)
        {
            throw new ArgumentNullException(nameof(in_items));
        }

        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Передачи НЗП");

        var rowIndex = 1;
        worksheet.Cell(rowIndex, 1).Value = "Отчёт по передаче НЗП за период";
        worksheet.Row(rowIndex).Style.Font.SetBold(true);
        worksheet.Row(rowIndex).Style.Font.FontSize = 14;
        rowIndex += 2;

        rowIndex = WriteFilterRow(worksheet, rowIndex, "Период", string.Format("{0:dd.MM.yyyy} — {1:dd.MM.yyyy}", in_filter.From, in_filter.To));

        if (!string.IsNullOrWhiteSpace(in_filter.Section))
        {
            rowIndex = WriteFilterRow(worksheet, rowIndex, "Вид работ", in_filter.Section!);
        }

        if (!string.IsNullOrWhiteSpace(in_filter.Part))
        {
            rowIndex = WriteFilterRow(worksheet, rowIndex, "Деталь", in_filter.Part!);
        }

        if (rowIndex > 3)
        {
            rowIndex++;
        }

        var headerColumnIndex = 1;
        worksheet.Cell(rowIndex, headerColumnIndex++).Value = "Деталь";
        

        foreach (var date in in_dates)
        {
            var cell = worksheet.Cell(rowIndex, headerColumnIndex++);
            cell.Value = date;
            cell.Style.DateFormat.Format = "dd.MM.yyyy";
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        worksheet.Row(rowIndex).Style.Font.SetBold(true);
        rowIndex++;

        if (in_items.Count == 0)
        {
            worksheet.Cell(rowIndex, 1).Value = "По выбранным условиям данные не найдены.";
            worksheet.Range(rowIndex, 1, rowIndex, Math.Max(3, in_dates.Count + 2)).Merge();
            worksheet.Row(rowIndex).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            worksheet.Row(rowIndex).Style.Font.SetItalic(true);
        }
        else
        {
            foreach (var item in in_items)
            {
                var columnIndex = 1;
                worksheet.Cell(rowIndex, columnIndex++).Value = item.PartName;
                

                foreach (var date in in_dates)
                {
                    var cell = worksheet.Cell(rowIndex, columnIndex++);
                    if (item.Cells.TryGetValue(date, out var values) && values.Count > 0)
                    {
                        var lines = values.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                        if (lines.Count > 0)
                        {
                            cell.Value = string.Join(Environment.NewLine, lines);
                            cell.Style.Alignment.WrapText = true;
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                        }
                        else
                        {
                            cell.Value = "—";
                            cell.Style.Font.SetFontColor(XLColor.Gray);
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                        }
                    }
                    else
                    {
                        cell.Value = "—";
                        cell.Style.Font.SetFontColor(XLColor.Gray);
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    }
                }

                rowIndex++;
            }
        }

        worksheet.Columns().AdjustToContents();
        worksheet.Rows().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var ret = stream.ToArray();
        return ret;
    }

    private static int WriteFilterRow(IXLWorksheet in_worksheet, int in_rowIndex, string in_label, string in_value)
    {
        in_worksheet.Cell(in_rowIndex, 1).Value = in_label;
        in_worksheet.Cell(in_rowIndex, 1).Style.Font.SetBold(true);
        in_worksheet.Cell(in_rowIndex, 2).Value = in_value;
        var ret = in_rowIndex + 1;
        return ret;
    }
}
