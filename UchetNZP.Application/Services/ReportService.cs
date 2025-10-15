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

        using var workbook = new XLWorkbook();
        var detailSheet = workbook.AddWorksheet("Запуски");

        detailSheet.Cell(1, 1).Value = "Дата";
        detailSheet.Cell(1, 2).Value = "Деталь";
        detailSheet.Cell(1, 3).Value = "От операции";
        detailSheet.Cell(1, 4).Value = "До операции";
        detailSheet.Cell(1, 5).Value = "Кол-во";
        detailSheet.Cell(1, 6).Value = "Операции хвоста";
        detailSheet.Cell(1, 7).Value = "Суммарные часы";

        var rowIndex = 2;
        foreach (var launch in launches)
        {
            var operations = launch.Operations
                .OrderBy(o => o.OpNumber)
                .ToList();

            var firstOp = operations.FirstOrDefault()?.OpNumber;
            var lastOp = operations.LastOrDefault()?.OpNumber;
            var operationsDescription = string.Join(
                ", ",
                operations.Select(o =>
                {
                    var norm = launch.Quantity == 0 ? 0 : o.Hours / launch.Quantity;
                    var opName = o.Operation?.Name ?? string.Empty;
                    return $"{o.OpNumber:D3} {opName} ({norm:0.###})".Trim();
                }));

            detailSheet.Cell(rowIndex, 1).Value = launch.LaunchDate;
            detailSheet.Cell(rowIndex, 1).Style.DateFormat.Format = "yyyy-MM-dd";
            detailSheet.Cell(rowIndex, 2).Value = launch.Part?.Name ?? string.Empty;
            detailSheet.Cell(rowIndex, 3).Value = firstOp.HasValue ? firstOp.Value.ToString("D3") : string.Empty;
            detailSheet.Cell(rowIndex, 4).Value = lastOp.HasValue ? lastOp.Value.ToString("D3") : string.Empty;
            detailSheet.Cell(rowIndex, 5).Value = launch.Quantity;
            detailSheet.Cell(rowIndex, 5).Style.NumberFormat.Format = "0.###";
            detailSheet.Cell(rowIndex, 6).Value = operationsDescription;
            detailSheet.Cell(rowIndex, 7).Value = launch.SumHoursToFinish;
            detailSheet.Cell(rowIndex, 7).Style.NumberFormat.Format = "0.###";

            rowIndex++;
        }

        var summarySheet = workbook.AddWorksheet("Сводка по операциям");
        summarySheet.Cell(1, 1).Value = "Операция";
        summarySheet.Cell(1, 2).Value = "Сумма часов";

        var summaryData = launches
            .SelectMany(x => x.Operations)
            .GroupBy(x => x.Operation?.Name ?? $"Операция {x.OpNumber:D3}")
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                OperationName = g.Key,
                SumHours = g.Sum(x => x.Hours),
            })
            .ToList();

        rowIndex = 2;
        foreach (var item in summaryData)
        {
            summarySheet.Cell(rowIndex, 1).Value = item.OperationName;
            summarySheet.Cell(rowIndex, 2).Value = item.SumHours;
            summarySheet.Cell(rowIndex, 2).Style.NumberFormat.Format = "0.###";
            rowIndex++;
        }

        detailSheet.Columns().AdjustToContents();
        summarySheet.Columns().AdjustToContents();

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return memoryStream.ToArray();
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
