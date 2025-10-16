using System;
using System.Linq;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
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

        var launches = await _dbContext.WipLaunches
            .AsNoTracking()
            .Where(x => x.LaunchDate >= fromUtc && x.LaunchDate <= toUtc)
            .Include(x => x.Part)
            .Include(x => x.Section)
            .Include(x => x.Operations)
                .ThenInclude(o => o.Operation)
            .OrderBy(x => x.LaunchDate)
            .ThenBy(x => x.Part!.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var groupedByDateAndSection = launches
            .GroupBy(x => new
            {
                Date = x.LaunchDate.Date,
                Section = x.Section?.Name ?? "Не указан"
            })
            .Select(g => new
            {
                g.Key.Date,
                Section = g.Key.Section,
                LaunchCount = g.Count(),
                Quantity = g.Sum(x => x.Quantity),
                Hours = g.Sum(x => x.SumHoursToFinish)
            })
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Section)
            .ToList();

        var totalsByDate = groupedByDateAndSection
            .GroupBy(x => x.Date)
            .ToDictionary(
                g => g.Key,
                g => new LaunchDateTotal(
                    g.Sum(x => x.LaunchCount),
                    g.Sum(x => x.Quantity),
                    g.Sum(x => x.Hours)));

        using var workbook = new XLWorkbook();
        var summarySheet = workbook.AddWorksheet("Запуски по датам");

        summarySheet.Cell(1, 1).Value = "Дата";
        summarySheet.Cell(1, 2).Value = "Участок";
        summarySheet.Cell(1, 3).Value = "Количество запусков";
        summarySheet.Cell(1, 4).Value = "Количество изделий";
        summarySheet.Cell(1, 5).Value = "Сумма часов";

        var rowIndex = 2;
        DateTime? currentDate = null;

        foreach (var item in groupedByDateAndSection)
        {
            if (currentDate.HasValue && currentDate.Value != item.Date)
            {
                WriteTotalRow(summarySheet, ref rowIndex, currentDate.Value, totalsByDate[currentDate.Value]);
            }

            currentDate = item.Date;

            var dateValue = DateTime.SpecifyKind(item.Date, DateTimeKind.Unspecified);
            summarySheet.Cell(rowIndex, 1).Value = dateValue;
            summarySheet.Cell(rowIndex, 1).Style.DateFormat.Format = "yyyy-MM-dd";
            summarySheet.Cell(rowIndex, 2).Value = item.Section;
            summarySheet.Cell(rowIndex, 3).Value = item.LaunchCount;
            summarySheet.Cell(rowIndex, 4).Value = item.Quantity;
            summarySheet.Cell(rowIndex, 4).Style.NumberFormat.Format = "0.###";
            summarySheet.Cell(rowIndex, 5).Value = item.Hours;
            summarySheet.Cell(rowIndex, 5).Style.NumberFormat.Format = "0.###";

            rowIndex++;
        }

        if (currentDate.HasValue)
        {
            WriteTotalRow(summarySheet, ref rowIndex, currentDate.Value, totalsByDate[currentDate.Value]);
        }

        summarySheet.Columns().AdjustToContents();

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return memoryStream.ToArray();
    }

    public async Task<byte[]> ExportLaunchesByDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var localDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Local);
        var nextLocalDate = localDate.AddDays(1);

        var fromUtc = localDate.ToUniversalTime();
        var toUtcExclusive = nextLocalDate.ToUniversalTime();

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

        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Запуски");

        worksheet.Cell(1, 1).Value = "Дата";
        var displayDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
        worksheet.Cell(1, 2).Value = displayDate;
        worksheet.Cell(1, 2).Style.DateFormat.Format = "dd.MM.yyyy";

        worksheet.Cell(3, 1).Value = "Деталь";
        worksheet.Cell(3, 2).Value = "Количество в запуске";
        worksheet.Cell(3, 3).Value = "Операция";
        worksheet.Cell(3, 4).Value = "Участок";
        worksheet.Cell(3, 5).Value = "Время на 1 деталь, ч";
        worksheet.Cell(3, 6).Value = "Количество незавершённых деталей";
        worksheet.Cell(3, 7).Value = "Часы по операции";

        var rowIndex = 4;
        decimal totalHours = 0m;
        var sectionSummaries = launches
            .SelectMany(x => x.Operations)
            .GroupBy(x => x.Section?.Name ?? "Участок не задан")
            .Select(g => new
            {
                Section = g.Key,
                Hours = g.Sum(o => o.Hours),
            })
            .OrderByDescending(x => x.Hours)
            .ThenBy(x => x.Section, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (launches.Count == 0)
        {
            worksheet.Cell(rowIndex, 1).Value = "За выбранную дату данных нет.";
            worksheet.Range(rowIndex, 1, rowIndex, 7).Merge();
            worksheet.Row(rowIndex).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        }
        else
        {
            for (var launchIndex = 0; launchIndex < launches.Count; launchIndex++)
            {
                var launch = launches[launchIndex];
                var isLastLaunch = launchIndex == launches.Count - 1;

                var partName = launch.Part?.Name ?? string.Empty;
                var partCode = launch.Part?.Code;
                var displayName = string.IsNullOrWhiteSpace(partCode) ? partName : $"{partName} ({partCode})";
                var quantity = launch.Quantity;

                var operations = launch.Operations
                    .OrderBy(o => o.OpNumber)
                    .Select(o => new
                    {
                        o.OpNumber,
                        SectionName = o.Section?.Name ?? string.Empty,
                        o.NormHours,
                        o.Quantity,
                        o.Hours,
                    })
                    .ToList();

                if (operations.Count == 0)
                {
                    var row = rowIndex++;
                    worksheet.Cell(row, 1).Value = displayName;
                    worksheet.Cell(row, 2).Value = quantity;
                    worksheet.Cell(row, 2).Style.NumberFormat.Format = "0.###";
                    worksheet.Cell(row, 3).Value = launch.FromOpNumber;
                    worksheet.Cell(row, 3).Style.NumberFormat.Format = "000";
                    worksheet.Cell(row, 4).Value = launch.Section?.Name ?? "Участок не задан";
                    worksheet.Cell(row, 5).Value = 0m;
                    worksheet.Cell(row, 5).Style.NumberFormat.Format = "0.###";
                    worksheet.Cell(row, 6).Value = quantity;
                    worksheet.Cell(row, 6).Style.NumberFormat.Format = "0.###";
                    worksheet.Cell(row, 7).Value = 0m;
                    worksheet.Cell(row, 7).Style.NumberFormat.Format = "0.###";
                }
                else
                {
                    for (var operationIndex = 0; operationIndex < operations.Count; operationIndex++)
                    {
                        var op = operations[operationIndex];
                        var row = rowIndex++;
                        var isFirstOperation = operationIndex == 0;

                        if (isFirstOperation)
                        {
                            worksheet.Cell(row, 1).Value = displayName;
                            worksheet.Cell(row, 2).Value = quantity;
                            worksheet.Cell(row, 2).Style.NumberFormat.Format = "0.###";
                        }
                        else
                        {
                            worksheet.Cell(row, 1).Value = string.Empty;
                            worksheet.Cell(row, 2).Value = string.Empty;
                        }

                        var sectionName = string.IsNullOrWhiteSpace(op.SectionName) ? "Участок не задан" : op.SectionName;
                        worksheet.Cell(row, 3).Value = op.OpNumber;
                        worksheet.Cell(row, 3).Style.NumberFormat.Format = "000";
                        worksheet.Cell(row, 4).Value = sectionName;
                        worksheet.Cell(row, 5).Value = op.NormHours;
                        worksheet.Cell(row, 5).Style.NumberFormat.Format = "0.###";

                        var remainingQuantity = op.Quantity;
                        if (remainingQuantity > 0m)
                        {
                            worksheet.Cell(row, 6).Value = remainingQuantity;
                            worksheet.Cell(row, 6).Style.NumberFormat.Format = "0.###";
                        }
                        else
                        {
                            worksheet.Cell(row, 6).Value = string.Empty;
                        }

                        var hours = op.Hours;
                        totalHours += hours;
                        worksheet.Cell(row, 7).Value = hours;
                        worksheet.Cell(row, 7).Style.NumberFormat.Format = "0.###";
                    }
                }

                if (!isLastLaunch)
                {
                    rowIndex++;
                }
            }
        }

        var summaryRow = rowIndex + 1;
        worksheet.Cell(summaryRow, 1).Value = "Итого часов";
        worksheet.Cell(summaryRow, 7).Value = totalHours;
        worksheet.Cell(summaryRow, 7).Style.NumberFormat.Format = "0.###";
        worksheet.Row(summaryRow).Style.Font.SetBold(true);

        if (sectionSummaries.Count > 0)
        {
            var sectionHeaderRow = summaryRow + 2;
            worksheet.Cell(sectionHeaderRow, 1).Value = "Часы по участкам";
            worksheet.Row(sectionHeaderRow).Style.Font.SetBold(true);

            var sectionRow = sectionHeaderRow + 1;
            foreach (var summary in sectionSummaries)
            {
                worksheet.Cell(sectionRow, 2).Value = summary.Section;
                worksheet.Cell(sectionRow, 3).Value = summary.Hours;
                worksheet.Cell(sectionRow, 3).Style.NumberFormat.Format = "0.###";
                sectionRow++;
            }
        }

        worksheet.Columns().AdjustToContents();

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return memoryStream.ToArray();
    }

    private static void WriteTotalRow(IXLWorksheet worksheet, ref int rowIndex, DateTime date, LaunchDateTotal total)
    {
        worksheet.Cell(rowIndex, 1).Clear();
        worksheet.Cell(rowIndex, 2).Value = $"Итого за {date:dd.MM.yyyy}";
        worksheet.Cell(rowIndex, 3).Value = total.LaunchCount;
        worksheet.Cell(rowIndex, 4).Value = total.Quantity;
        worksheet.Cell(rowIndex, 4).Style.NumberFormat.Format = "0.###";
        worksheet.Cell(rowIndex, 5).Value = total.Hours;
        worksheet.Cell(rowIndex, 5).Style.NumberFormat.Format = "0.###";
        worksheet.Row(rowIndex).Style.Font.SetBold(true);

        rowIndex++;
        worksheet.Row(rowIndex).Clear();
        rowIndex++;
    }

    private sealed record LaunchDateTotal(int LaunchCount, decimal Quantity, decimal Hours);

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
