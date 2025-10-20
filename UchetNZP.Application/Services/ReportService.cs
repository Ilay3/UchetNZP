using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Shared;

namespace UchetNZP.Application.Services;

public class ReportService : IReportService
{
    private readonly AppDbContext _dbContext;

    public ReportService(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<byte[]> ExportRoutesToExcelAsync(string? search, Guid? sectionId, CancellationToken cancellationToken = default)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        Guid? normalizedSectionId = null;

        if (sectionId.HasValue && sectionId.Value != Guid.Empty)
        {
            normalizedSectionId = sectionId.Value;
        }

        var routesQuery = from route in _dbContext.PartRoutes.AsNoTracking()
                          join part in _dbContext.Parts.AsNoTracking() on route.PartId equals part.Id
                          join operation in _dbContext.Operations.AsNoTracking() on route.OperationId equals operation.Id into operations
                          from operation in operations.DefaultIfEmpty()
                          join section in _dbContext.Sections.AsNoTracking() on route.SectionId equals section.Id into sections
                          from section in sections.DefaultIfEmpty()
                          select new { route, part, operation, section };

        if (!string.IsNullOrEmpty(normalizedSearch))
        {
            var term = normalizedSearch.ToLowerInvariant();
            routesQuery = routesQuery.Where(x =>
                x.part.Name.ToLower().Contains(term) ||
                (x.part.Code != null && x.part.Code.ToLower().Contains(term)) ||
                (x.operation != null && x.operation.Name.ToLower().Contains(term)) ||
                (x.section != null && x.section.Name.ToLower().Contains(term)));
        }

        if (normalizedSectionId.HasValue)
        {
            var sectionFilterId = normalizedSectionId.Value;
            routesQuery = routesQuery.Where(x => x.section != null && x.section.Id == sectionFilterId);
        }

        var routes = await routesQuery
            .OrderBy(x => x.part.Name)
            .ThenBy(x => x.route.OpNumber)
            .Select(x => new RouteExportRow(
                x.part.Name,
                x.part.Code,
                x.route.OpNumber,
                x.operation != null ? x.operation.Name : string.Empty,
                x.section != null ? x.section.Name : string.Empty,
                x.route.NormHours))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        string? sectionDisplay = null;

        if (normalizedSectionId.HasValue)
        {
            sectionDisplay = await _dbContext.Sections
                .AsNoTracking()
                .Where(x => x.Id == normalizedSectionId.Value)
                .Select(x => string.IsNullOrWhiteSpace(x.Code) ? x.Name : $"{x.Name} ({x.Code})")
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return BuildRoutesWorkbook(routes, normalizedSearch, sectionDisplay);
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

    private static byte[] BuildRoutesWorkbook(
        IReadOnlyList<RouteExportRow> routes,
        string? search,
        string? sectionDisplay)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Маршруты");

        var rowIndex = 1;
        worksheet.Cell(rowIndex, 1).Value = "Маршруты деталей";
        worksheet.Row(rowIndex).Style.Font.SetBold(true);
        worksheet.Row(rowIndex).Style.Font.FontSize = 14;
        rowIndex += 2;

        if (!string.IsNullOrWhiteSpace(search))
        {
            rowIndex = WriteFilterRow(worksheet, rowIndex, "Поиск", search);
        }

        if (!string.IsNullOrWhiteSpace(sectionDisplay))
        {
            rowIndex = WriteFilterRow(worksheet, rowIndex, "Участок", sectionDisplay!);
        }

        if (rowIndex > 3)
        {
            rowIndex++;
        }

        worksheet.Cell(rowIndex, 1).Value = "Деталь";
        worksheet.Cell(rowIndex, 2).Value = "Код детали";
        worksheet.Cell(rowIndex, 3).Value = "№ операции";
        worksheet.Cell(rowIndex, 4).Value = "Операция";
        worksheet.Cell(rowIndex, 5).Value = "Участок";
        worksheet.Cell(rowIndex, 6).Value = "Норматив, н/ч";
        worksheet.Row(rowIndex).Style.Font.SetBold(true);
        rowIndex++;

        if (routes.Count == 0)
        {
            worksheet.Cell(rowIndex, 1).Value = "По выбранным условиям данные не найдены.";
            worksheet.Row(rowIndex).Style.Font.SetItalic(true);
        }
        else
        {
            foreach (var route in routes)
            {
                worksheet.Cell(rowIndex, 1).Value = route.PartName;
                worksheet.Cell(rowIndex, 2).Value = route.PartCode ?? string.Empty;
                worksheet.Cell(rowIndex, 3).Value = OperationNumber.Format(route.OpNumber);
                worksheet.Cell(rowIndex, 4).Value = route.OperationName;
                worksheet.Cell(rowIndex, 5).Value = route.SectionName;
                worksheet.Cell(rowIndex, 6).Value = route.NormHours;
                worksheet.Cell(rowIndex, 6).Style.NumberFormat.Format = "0.###";
                rowIndex++;
            }
        }

        worksheet.Columns().AdjustToContents();

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return memoryStream.ToArray();
    }

    private static int WriteFilterRow(IXLWorksheet worksheet, int rowIndex, string label, string value)
    {
        worksheet.Cell(rowIndex, 1).Value = label;
        worksheet.Cell(rowIndex, 1).Style.Font.SetBold(true);
        worksheet.Cell(rowIndex, 2).Value = value;
        return rowIndex + 1;
    }

    private sealed record RouteExportRow(
        string PartName,
        string? PartCode,
        int OpNumber,
        string OperationName,
        string SectionName,
        decimal NormHours);

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
            var sectionHoursTotals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            foreach (var launch in launches)
            {
                rowIndex = WriteLaunchBlock(worksheet, rowIndex, launch, balanceLookup, sectionHoursTotals);
                rowIndex += 2;
            }

            if (sectionHoursTotals.Count > 0)
            {
                rowIndex++;
                worksheet.Cell(rowIndex, 1).Value = "Сумма времени по участкам";
                worksheet.Cell(rowIndex, 1).Style.Font.SetBold(true);
                rowIndex++;

                foreach (var section in sectionHoursTotals.OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase))
                {
                    worksheet.Cell(rowIndex, 1).Value = section.Key;
                    worksheet.Cell(rowIndex, 2).Value = section.Value;
                    worksheet.Cell(rowIndex, 2).Style.NumberFormat.Format = "0.###";
                    rowIndex++;
                }
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
        IReadOnlyDictionary<(Guid PartId, Guid SectionId, int OpNumber), decimal> balanceLookup,
        IDictionary<string, decimal> sectionHoursTotals)
    {
        var row = startRow;

        var partName = launch.Part?.Name ?? string.Empty;
        var partCode = launch.Part?.Code ?? string.Empty;
        var partDisplay = string.IsNullOrWhiteSpace(partName)
            ? partCode
            : partName;
        partDisplay = RemoveParenthetical(partDisplay);
        var comment = string.IsNullOrWhiteSpace(launch.Comment) ? null : launch.Comment;

        if (!string.IsNullOrWhiteSpace(comment))
        {
            WriteLabelValue(worksheet, row++, "Комментарий", comment);
            row++;
        }

        var operations = launch.Operations
            .OrderBy(o => o.OpNumber)
            .Select(o => new
            {
                o.OpNumber,
                OperationName = o.Operation != null ? o.Operation.Name : string.Empty,
                o.SectionId,
                SectionName = o.Section != null ? o.Section.Name : string.Empty,
                o.NormHours,
                o.Hours,
                o.Quantity
            })
            .ToList();

        var operationsHeaderRow = row;
        const int detailColumn = 1;
        const int labelColumn = 2;
        const int firstDataColumn = 3;

        var launchQuantity = launch.Quantity;

        var launchQuantitiesByOperation = operations
            .Where(o => o.Quantity > 0m)
            .GroupBy(o => o.OpNumber)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var launchQuantityTargets = new HashSet<int>();
        decimal? uniformLaunchQuantity = null;

        if (launchQuantitiesByOperation.Count == 0)
        {
            if (launchQuantity > 0m)
            {
                launchQuantityTargets.Add(launch.FromOpNumber);
                uniformLaunchQuantity = launchQuantity;
            }
        }
        else
        {
            var operationsWithPositiveQuantity = launchQuantitiesByOperation.Count;
            if (operationsWithPositiveQuantity == operations.Count && launchQuantitiesByOperation.Values.Distinct().Count() == 1)
            {
                launchQuantityTargets.Add(launch.FromOpNumber);
                uniformLaunchQuantity = launchQuantitiesByOperation.Values.First();
            }
            else
            {
                foreach (var opNumber in launchQuantitiesByOperation.Keys)
                {
                    launchQuantityTargets.Add(opNumber);
                }
            }
        }

        worksheet.Cell(operationsHeaderRow, detailColumn).Value = partDisplay;
        worksheet.Cell(operationsHeaderRow, labelColumn).Value = "Операция";
        worksheet.Cell(operationsHeaderRow + 1, labelColumn).Value = "Норма, н/ч";
        worksheet.Cell(operationsHeaderRow + 2, labelColumn).Value = "Количество на операции";
        worksheet.Cell(operationsHeaderRow + 3, labelColumn).Value = "Количество запуска";
        worksheet.Range(operationsHeaderRow, labelColumn, operationsHeaderRow + 3, labelColumn).Style.Font.SetBold(true);
        worksheet.Cell(operationsHeaderRow, detailColumn).Style.Font.SetBold(true);

        var column = firstDataColumn;

        decimal totalNormHours = 0m;
        decimal totalOperationQuantity = 0m;

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
                var operationName = RemoveParenthetical(operation.OperationName);
                var operationDisplay = string.IsNullOrWhiteSpace(operationName)
                    ? $"{operation.OpNumber:000}"
                    : $"{operation.OpNumber:000} — {operationName}";

                worksheet.Cell(operationsHeaderRow, column).Value = operationDisplay;
                worksheet.Cell(operationsHeaderRow + 1, column).Value = operation.NormHours;
                worksheet.Cell(operationsHeaderRow + 1, column).Style.NumberFormat.Format = "0.###";
                totalNormHours += operation.NormHours;

                if (operation.SectionId != Guid.Empty &&
                    balanceLookup.TryGetValue((launch.PartId, operation.SectionId, operation.OpNumber), out var balance) &&
                    balance > 0m)
                {
                    worksheet.Cell(operationsHeaderRow + 2, column).Value = balance;
                    worksheet.Cell(operationsHeaderRow + 2, column).Style.NumberFormat.Format = "0.###";
                    totalOperationQuantity += balance;
                }

                if (launchQuantityTargets.Contains(operation.OpNumber))
                {
                    var quantityToDisplay = launchQuantitiesByOperation.TryGetValue(operation.OpNumber, out var operationLaunchQuantity)
                        ? operationLaunchQuantity
                        : uniformLaunchQuantity ?? launchQuantity;

                    if (quantityToDisplay > 0m)
                    {
                        worksheet.Cell(operationsHeaderRow + 3, column).Value = quantityToDisplay;
                        worksheet.Cell(operationsHeaderRow + 3, column).Style.NumberFormat.Format = "0.###";
                    }
                }

                var sectionKey = string.IsNullOrWhiteSpace(operation.SectionName)
                    ? "Участок не задан"
                    : operation.SectionName;
                if (!sectionHoursTotals.TryGetValue(sectionKey, out var sectionTotal))
                {
                    sectionTotal = 0m;
                }

                sectionTotal += operation.Hours;
                sectionHoursTotals[sectionKey] = sectionTotal;

                column++;
            }

            worksheet.Cell(operationsHeaderRow, column).Value = "Итоги";
            worksheet.Cell(operationsHeaderRow, column).Style.Font.SetBold(true);

            worksheet.Cell(operationsHeaderRow + 1, column).Value = totalNormHours;
            worksheet.Cell(operationsHeaderRow + 1, column).Style.NumberFormat.Format = "0.###";

            if (totalOperationQuantity > 0m)
            {
                worksheet.Cell(operationsHeaderRow + 2, column).Value = totalOperationQuantity;
                worksheet.Cell(operationsHeaderRow + 2, column).Style.NumberFormat.Format = "0.###";
            }

            if (launchQuantity > 0m)
            {
                worksheet.Cell(operationsHeaderRow + 3, column).Value = launchQuantity;
                worksheet.Cell(operationsHeaderRow + 3, column).Style.NumberFormat.Format = "0.###";
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

    private static string RemoveParenthetical(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var depth = 0;

        foreach (var symbol in value)
        {
            if (symbol == '(')
            {
                depth++;
                continue;
            }

            if (symbol == ')')
            {
                if (depth > 0)
                {
                    depth--;
                    continue;
                }
            }

            if (depth == 0)
            {
                builder.Append(symbol);
            }
        }

        var result = builder.ToString().Trim();
        return string.Join(' ', result.Split(' ', StringSplitOptions.RemoveEmptyEntries));
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
