using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;

namespace UchetNZP.Application.Services;

public class ReportService : IReportService
{
    private readonly AppDbContext _dbContext;

    public ReportService(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<byte[]> ExportLaunchesToExcelAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var fromUtc = NormalizeToUtc(from);
        var toUtc = NormalizeToUtc(to);

        if (fromUtc > toUtc)
        {
            throw new ArgumentException("Дата начала периода не может быть больше даты окончания.", nameof(from));
        }

        var toUtcExclusive = toUtc == DateTime.MaxValue ? toUtc : toUtc.AddTicks(1);

        var launches = await GetLaunchesAsync(fromUtc, toUtcExclusive, cancellationToken).ConfigureAwait(false);
        var balanceLookup = await BuildBalanceLookupAsync(launches, cancellationToken).ConfigureAwait(false);

        var fromLocal = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc).ToLocalTime();
        var toLocal = DateTime.SpecifyKind(toUtc, DateTimeKind.Utc).ToLocalTime();

        return BuildLaunchesWorkbook(
            launches,
            balanceLookup,
            $"Запуски НЗП за период {fromLocal:dd.MM.yyyy} - {toLocal:dd.MM.yyyy}",
            fromLocal,
            toLocal);
    }

    public async Task<byte[]> ExportLaunchesByDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var localDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Local);
        var nextLocalDate = localDate.AddDays(1);

        var fromUtc = localDate.ToUniversalTime();
        var toUtcExclusive = nextLocalDate.ToUniversalTime();

        var launches = await GetLaunchesAsync(fromUtc, toUtcExclusive, cancellationToken).ConfigureAwait(false);
        var balanceLookup = await BuildBalanceLookupAsync(launches, cancellationToken).ConfigureAwait(false);

        var displayDate = DateTime.SpecifyKind(localDate, DateTimeKind.Local);

        return BuildLaunchesWorkbook(
            launches,
            balanceLookup,
            $"Запуски НЗП за {displayDate:dd.MM.yyyy}",
            displayDate,
            displayDate);
    }

    private async Task<List<WipLaunch>> GetLaunchesAsync(DateTime fromUtc, DateTime toUtcExclusive, CancellationToken cancellationToken)
    {
        var launches = await _dbContext.WipLaunches
            .AsNoTracking()
            .Where(x => x.LaunchDate >= fromUtc && x.LaunchDate < toUtcExclusive)
            .Include(x => x.Part)
            .Include(x => x.Section)
            .Include(x => x.Operations)
                .ThenInclude(o => o.Operation)
            .Include(x => x.Operations)
                .ThenInclude(o => o.Section)
            .OrderBy(x => x.LaunchDate)
            .ThenBy(x => x.Part != null ? x.Part.Name : string.Empty)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return launches;
    }

    private static byte[] BuildLaunchesWorkbook(
        IReadOnlyList<WipLaunch> launches,
        IReadOnlyDictionary<(Guid PartId, Guid SectionId, int OpNumber), decimal> balanceLookup,
        string title,
        DateTime? periodFrom,
        DateTime? periodTo)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Запуски");

        var rowIndex = 1;
        worksheet.Cell(rowIndex, 1).Value = title;
        worksheet.Row(rowIndex).Style.Font.SetBold(true);
        worksheet.Row(rowIndex).Style.Font.FontSize = 14;
        rowIndex += 2;

        if (periodFrom.HasValue && periodTo.HasValue)
        {
            worksheet.Cell(rowIndex, 1).Value = "Период";
            worksheet.Cell(rowIndex, 1).Style.Font.SetBold(true);
            if (periodFrom.Value.Date == periodTo.Value.Date)
            {
                var display = DateTime.SpecifyKind(periodFrom.Value.Date, DateTimeKind.Unspecified);
                worksheet.Cell(rowIndex, 2).Value = display;
                worksheet.Cell(rowIndex, 2).Style.DateFormat.Format = "dd.MM.yyyy";
            }
            else
            {
                var fromDisplay = DateTime.SpecifyKind(periodFrom.Value.Date, DateTimeKind.Unspecified);
                var toDisplay = DateTime.SpecifyKind(periodTo.Value.Date, DateTimeKind.Unspecified);
                worksheet.Cell(rowIndex, 2).Value = fromDisplay;
                worksheet.Cell(rowIndex, 2).Style.DateFormat.Format = "dd.MM.yyyy";
                worksheet.Cell(rowIndex, 3).Value = toDisplay;
                worksheet.Cell(rowIndex, 3).Style.DateFormat.Format = "dd.MM.yyyy";
            }

            rowIndex += 2;
        }

        if (launches.Count == 0)
        {
            worksheet.Cell(rowIndex, 1).Value = "За выбранный период данных нет.";
            worksheet.Row(rowIndex).Style.Font.SetItalic(true);
            worksheet.Row(rowIndex).Style.Font.FontColor = XLColor.FromTheme(XLThemeColor.Text1, 0.5);
        }
        else
        {
            foreach (var launch in launches)
            {
                rowIndex = WriteLaunchBlock(worksheet, rowIndex, launch, balanceLookup);
                rowIndex += 2;
            }
        }

        worksheet.Columns().AdjustToContents();

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return memoryStream.ToArray();
    }

    private static int WriteLaunchBlock(
        IXLWorksheet worksheet,
        int startRow,
        WipLaunch launch,
        IReadOnlyDictionary<(Guid PartId, Guid SectionId, int OpNumber), decimal> balanceLookup)
    {
        var row = startRow;

        var launchDateLocal = ConvertToLocal(launch.LaunchDate);
        var partName = launch.Part?.Name ?? string.Empty;
        var partCode = launch.Part?.Code;
        var partDisplay = string.IsNullOrWhiteSpace(partCode) ? partName : $"{partName} ({partCode})";
        var sectionName = launch.Section?.Name ?? "Участок не задан";
        var comment = string.IsNullOrWhiteSpace(launch.Comment) ? null : launch.Comment;

        WriteLabelValue(worksheet, row++, "Дата запуска", launchDateLocal, cell => cell.Style.DateFormat.Format = "dd.MM.yyyy HH:mm");
        WriteLabelValue(worksheet, row++, "ID запуска", launch.Id);
        WriteLabelValue(worksheet, row++, "Деталь", partDisplay);
        WriteLabelValue(worksheet, row++, "Участок", sectionName);
        WriteLabelValue(worksheet, row++, "Операция запуска", launch.FromOpNumber, cell => cell.Style.NumberFormat.Format = "000");
        WriteLabelValue(worksheet, row++, "Количество запуска", launch.Quantity, cell => cell.Style.NumberFormat.Format = "0.###");
        WriteLabelValue(worksheet, row++, "Часы до завершения", launch.SumHoursToFinish, cell => cell.Style.NumberFormat.Format = "0.###");

        if (!string.IsNullOrWhiteSpace(comment))
        {
            WriteLabelValue(worksheet, row++, "Комментарий", comment);
        }

        row++;

        var operations = launch.Operations
            .OrderBy(o => o.OpNumber)
            .Select(o => new
            {
                o.OpNumber,
                OperationName = o.Operation != null ? o.Operation.Name : string.Empty,
                o.SectionId,
                SectionName = o.Section != null ? o.Section.Name : string.Empty,
                o.NormHours,
                o.Hours
            })
            .ToList();

        var operationsHeaderRow = row;
        worksheet.Cell(operationsHeaderRow, 1).Value = "Операция";
        worksheet.Cell(operationsHeaderRow + 1, 1).Value = "Норма, н/ч";
        worksheet.Cell(operationsHeaderRow + 2, 1).Value = "Количество на операции";
        worksheet.Cell(operationsHeaderRow + 3, 1).Value = "Часы по операции";
        worksheet.Range(operationsHeaderRow, 1, operationsHeaderRow + 3, 1).Style.Font.SetBold(true);

        var column = 2;

        if (operations.Count == 0)
        {
            worksheet.Cell(operationsHeaderRow, column).Value = "Операции не найдены";
            worksheet.Range(operationsHeaderRow, column, operationsHeaderRow, column + 1).Merge();
            worksheet.Row(operationsHeaderRow).Style.Font.SetItalic(true);
        }
        else
        {
            foreach (var operation in operations)
            {
                var operationDisplay = string.IsNullOrWhiteSpace(operation.OperationName)
                    ? $"{operation.OpNumber:000}"
                    : $"{operation.OpNumber:000} — {operation.OperationName}";

                if (!string.IsNullOrWhiteSpace(operation.SectionName))
                {
                    operationDisplay += $" ({operation.SectionName})";
                }

                worksheet.Cell(operationsHeaderRow, column).Value = operationDisplay;
                worksheet.Cell(operationsHeaderRow + 1, column).Value = operation.NormHours;
                worksheet.Cell(operationsHeaderRow + 1, column).Style.NumberFormat.Format = "0.###";

                if (operation.SectionId != Guid.Empty &&
                    balanceLookup.TryGetValue((launch.PartId, operation.SectionId, operation.OpNumber), out var balance) &&
                    balance > 0m)
                {
                    worksheet.Cell(operationsHeaderRow + 2, column).Value = balance;
                    worksheet.Cell(operationsHeaderRow + 2, column).Style.NumberFormat.Format = "0.###";
                }

                worksheet.Cell(operationsHeaderRow + 3, column).Value = operation.Hours;
                worksheet.Cell(operationsHeaderRow + 3, column).Style.NumberFormat.Format = "0.###";

                column++;
            }
        }

        return operationsHeaderRow + 4;
    }

    private static void WriteLabelValue(
        IXLWorksheet worksheet,
        int row,
        string label,
        object? value,
        Action<IXLCell>? configure = null)
    {
        worksheet.Cell(row, 1).Value = label;
        worksheet.Cell(row, 1).Style.Font.SetBold(true);
        var cell = worksheet.Cell(row, 2);
        switch (value)
        {
            case null:
                cell.Clear();
                break;
            case DateTime dateValue:
                cell.Value = dateValue;
                break;
            case decimal decimalValue:
                cell.Value = decimalValue;
                break;
            case int intValue:
                cell.Value = intValue;
                break;
            case Guid guidValue:
                cell.Value = guidValue.ToString();
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
        configure?.Invoke(cell);
    }

    private static DateTime ConvertToLocal(DateTime value)
    {
        var utcValue = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        var local = utcValue.ToLocalTime();
        return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
    }

    private async Task<Dictionary<(Guid PartId, Guid SectionId, int OpNumber), decimal>> BuildBalanceLookupAsync(
        IReadOnlyCollection<WipLaunch> launches,
        CancellationToken cancellationToken)
    {
        if (launches.Count == 0)
        {
            return new();
        }

        var partIds = launches
            .Select(x => x.PartId)
            .Distinct()
            .ToList();

        var opNumbers = launches
            .SelectMany(x => x.Operations)
            .Select(x => x.OpNumber)
            .Distinct()
            .ToList();

        if (partIds.Count == 0 || opNumbers.Count == 0)
        {
            return new();
        }

        var balances = await _dbContext.WipBalances
            .AsNoTracking()
            .Where(x => partIds.Contains(x.PartId) && opNumbers.Contains(x.OpNumber))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new Dictionary<(Guid PartId, Guid SectionId, int OpNumber), decimal>(balances.Count);

        foreach (var balance in balances)
        {
            result[(balance.PartId, balance.SectionId, balance.OpNumber)] = balance.Quantity;
        }

        return result;
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }
}
