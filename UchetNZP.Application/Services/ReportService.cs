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
