using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using UchetNZP.Web.Models;

namespace UchetNZP.Web.Services;

public interface IWipHistoryExcelExporter
{
    byte[] Export(IReadOnlyList<WipHistoryEntryViewModel> entries);
}

public class WipHistoryExcelExporter : IWipHistoryExcelExporter
{
    public byte[] Export(IReadOnlyList<WipHistoryEntryViewModel> entries)
    {
        if (entries is null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("История НЗП");

        worksheet.Cell(1, 1).Value = "Дата/время";
        worksheet.Cell(1, 2).Value = "Тип операции";
        worksheet.Cell(1, 3).Value = "Деталь";
        worksheet.Cell(1, 4).Value = "Операция от/куда";
        worksheet.Cell(1, 5).Value = "Количество";
        worksheet.Cell(1, 6).Value = "Статус/отмена";
        worksheet.Cell(1, 7).Value = "Комментарий";
        worksheet.Cell(1, 8).Value = "Пользователь";

        worksheet.Row(1).Style.Font.SetBold(true);
        worksheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8f9fa");
        worksheet.Row(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var rowIndex = 2;
        foreach (var entry in entries)
        {
            worksheet.Cell(rowIndex, 1).Value = entry.OccurredAt;
            worksheet.Cell(rowIndex, 1).Style.DateFormat.Format = "dd.MM.yyyy HH:mm";

            worksheet.Cell(rowIndex, 2).Value = entry.TypeDisplayName;
            worksheet.Cell(rowIndex, 3).Value = entry.PartDisplayName;
            worksheet.Cell(rowIndex, 4).Value = BuildOperationPath(entry);
            worksheet.Cell(rowIndex, 5).Value = entry.Quantity;
            worksheet.Cell(rowIndex, 5).Style.NumberFormat.Format = "0.###";
            worksheet.Cell(rowIndex, 6).Value = entry.IsCancelled ? "Отменено" : "Проведено";
            worksheet.Cell(rowIndex, 7).Value = entry.Comment ?? string.Empty;
            worksheet.Cell(rowIndex, 8).Value = entry.UserDisplay ?? string.Empty;
            rowIndex++;
        }

        worksheet.Columns().AdjustToContents();
        worksheet.Column(7).Width = Math.Max(worksheet.Column(7).Width, 30);
        worksheet.Column(4).Width = Math.Max(worksheet.Column(4).Width, 40);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string BuildOperationPath(WipHistoryEntryViewModel entry)
    {
        var fromSection = string.IsNullOrWhiteSpace(entry.SectionName) ? string.Empty : entry.SectionName.Trim();
        var toSection = string.IsNullOrWhiteSpace(entry.TargetSectionName) ? string.Empty : entry.TargetSectionName.Trim();
        var operationPath = string.IsNullOrWhiteSpace(entry.FullOperationPath)
            ? entry.OperationRange ?? string.Empty
            : entry.FullOperationPath;

        if (!string.IsNullOrWhiteSpace(fromSection) && !string.IsNullOrWhiteSpace(toSection))
        {
            return string.IsNullOrWhiteSpace(operationPath)
                ? $"{fromSection} → {toSection}"
                : $"{fromSection} → {toSection} ({operationPath})";
        }

        if (!string.IsNullOrWhiteSpace(fromSection))
        {
            return string.IsNullOrWhiteSpace(operationPath)
                ? fromSection
                : $"{fromSection} ({operationPath})";
        }

        if (!string.IsNullOrWhiteSpace(toSection))
        {
            return string.IsNullOrWhiteSpace(operationPath)
                ? toSection
                : $"{toSection} ({operationPath})";
        }

        return operationPath;
    }
}
