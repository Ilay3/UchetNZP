using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Models;
using UchetNZP.Web.Services;
using UchetNZP.Shared;

namespace UchetNZP.Web.Controllers;

[Route("reports")]
public class ReportsController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly IScrapReportExcelExporter _scrapReportExcelExporter;
    private readonly ITransferPeriodReportExcelExporter _transferPeriodReportExcelExporter;

    public ReportsController(
        AppDbContext dbContext,
        IScrapReportExcelExporter scrapReportExcelExporter,
        ITransferPeriodReportExcelExporter transferPeriodReportExcelExporter)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _scrapReportExcelExporter = scrapReportExcelExporter ?? throw new ArgumentNullException(nameof(scrapReportExcelExporter));
        _transferPeriodReportExcelExporter = transferPeriodReportExcelExporter ?? throw new ArgumentNullException(nameof(transferPeriodReportExcelExporter));
    }

    [HttpGet("receipts")]
    public async Task<IActionResult> ReceiptReport([FromQuery] ReceiptReportQuery? query, CancellationToken cancellationToken)
    {
        var now = DateTime.Now.Date;
        var defaultFrom = now.AddDays(-29);
        var (fromDate, toDate) = NormalizePeriod(query?.From ?? defaultFrom, query?.To ?? now);

        var fromUtc = ToUtcStartOfDay(fromDate);
        var toUtcExclusive = ToUtcStartOfDay(toDate.AddDays(1));

        IQueryable<WipReceipt> receiptsQuery = _dbContext.WipReceipts
            .AsNoTracking()
            .Where(x => x.ReceiptDate >= fromUtc && x.ReceiptDate < toUtcExclusive);

        if (!string.IsNullOrWhiteSpace(query?.Section))
        {
            var term = query.Section.Trim().ToLowerInvariant();
            receiptsQuery = receiptsQuery.Where(x =>
                x.Section != null &&
                (x.Section.Name.ToLower().Contains(term) ||
                 (x.Section.Code != null && x.Section.Code.ToLower().Contains(term))));
        }

        if (!string.IsNullOrWhiteSpace(query?.Part))
        {
            var term = query.Part.Trim().ToLowerInvariant();
            receiptsQuery = receiptsQuery.Where(x =>
                x.Part != null &&
                (x.Part.Name.ToLower().Contains(term) ||
                 (x.Part.Code != null && x.Part.Code.ToLower().Contains(term))));
        }

        if (query?.MinQuantity is decimal minQuantity)
        {
            receiptsQuery = receiptsQuery.Where(x => x.Quantity >= minQuantity);
        }

        if (query?.MaxQuantity is decimal maxQuantity)
        {
            receiptsQuery = receiptsQuery.Where(x => x.Quantity <= maxQuantity);
        }

        var receipts = await receiptsQuery
            .Include(x => x.Part)
            .Include(x => x.Section)
            .Include(x => x.WipLabel)
            .OrderByDescending(x => x.ReceiptDate)
            .ThenBy(x => x.Part != null ? x.Part.Name : string.Empty)
            .ThenBy(x => x.OpNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = receipts
            .Select(x => new ReceiptReportItemViewModel(
                ConvertToLocal(x.ReceiptDate),
                x.Section != null ? x.Section.Name : "Вид работ не задан",
                x.Part != null ? x.Part.Name : string.Empty,
                x.Part?.Code,
                OperationNumber.Format(x.OpNumber),
                x.Quantity,
                string.IsNullOrWhiteSpace(x.Comment) ? null : x.Comment,
                x.WipLabel != null && !string.IsNullOrWhiteSpace(x.WipLabel.Number) ? x.WipLabel.Number : null))
            .ToList();

        var filter = new ReceiptReportFilterViewModel
        {
            From = DateTime.SpecifyKind(fromDate, DateTimeKind.Unspecified),
            To = DateTime.SpecifyKind(toDate, DateTimeKind.Unspecified),
            Section = query?.Section,
            Part = query?.Part,
            MinQuantity = query?.MinQuantity,
            MaxQuantity = query?.MaxQuantity,
        };

        var model = new ReceiptReportViewModel(
            filter,
            items,
            items.Sum(x => x.Quantity));

        return View("~/Views/Reports/ReceiptReport.cshtml", model);
    }

    [HttpGet("scrap")]
    public async Task<IActionResult> ScrapReport([FromQuery] ScrapReportQuery? query, CancellationToken cancellationToken)
    {
        var (filter, items) = await LoadScrapReportAsync(query, cancellationToken).ConfigureAwait(false);
        var model = new ScrapReportViewModel(filter, items, items.Sum(x => x.Quantity));
        return View("~/Views/Reports/ScrapReport.cshtml", model);
    }

    [HttpGet("scrap/export")]
    public async Task<IActionResult> ScrapReportExport([FromQuery] ScrapReportQuery? query, CancellationToken cancellationToken)
    {
        var (filter, items) = await LoadScrapReportAsync(query, cancellationToken).ConfigureAwait(false);
        var content = _scrapReportExcelExporter.Export(filter, items);
        var fileName = $"scrap-report-{filter.From:yyyyMMdd}-{filter.To:yyyyMMdd}.xlsx";
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("transfer-period")]
    public async Task<IActionResult> TransferPeriodReport([FromQuery] TransferPeriodReportQuery? in_query, CancellationToken in_cancellationToken)
    {
        var model = await LoadTransferPeriodReportAsync(in_query, in_cancellationToken).ConfigureAwait(false);
        IActionResult ret = View("~/Views/Reports/TransferPeriodReport.cshtml", model);
        return ret;
    }

    [HttpGet("transfer-period/export")]
    public async Task<IActionResult> TransferPeriodReportExport([FromQuery] TransferPeriodReportQuery? in_query, CancellationToken in_cancellationToken)
    {
        var model = await LoadTransferPeriodReportAsync(in_query, in_cancellationToken).ConfigureAwait(false);
        var content = _transferPeriodReportExcelExporter.Export(model.Filter, model.Dates, model.Items);
        var fileName = string.Format(
            "transfer-period-report-{0:yyyyMMdd}-{1:yyyyMMdd}.xlsx",
            model.Filter.From,
            model.Filter.To);
        IActionResult ret = File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        return ret;
    }

    private async Task<TransferPeriodReportViewModel> LoadTransferPeriodReportAsync(TransferPeriodReportQuery? in_query, CancellationToken in_cancellationToken)
    {
        var now = DateTime.Now.Date;
        var defaultFrom = now.AddDays(-29);
        var period = NormalizePeriod(in_query?.From ?? defaultFrom, in_query?.To ?? now);
        var fromDate = period.From;
        var toDate = period.To;

        var fromUtc = ToUtcStartOfDay(fromDate);
        var toUtcExclusive = ToUtcStartOfDay(toDate.AddDays(1));

        IQueryable<WipTransfer> transfersQuery = _dbContext.WipTransfers
            .AsNoTracking()
            .Where(x => x.TransferDate >= fromUtc && x.TransferDate < toUtcExclusive)
            .Include(x => x.Part)
            .Include(x => x.Operations)
                .ThenInclude(x => x.Section);

        if (!string.IsNullOrWhiteSpace(in_query?.Part))
        {
            var partTerm = in_query.Part.Trim().ToLowerInvariant();
            transfersQuery = transfersQuery.Where(x =>
                x.Part != null &&
                (x.Part.Name.ToLower().Contains(partTerm) ||
                 (x.Part.Code != null && x.Part.Code.ToLower().Contains(partTerm))));
        }

        if (!string.IsNullOrWhiteSpace(in_query?.Section))
        {
            var sectionTerm = in_query.Section.Trim().ToLowerInvariant();
            transfersQuery = transfersQuery.Where(x =>
                x.Operations.Any(operation =>
                    operation.Section != null &&
                    (operation.Section.Name.ToLower().Contains(sectionTerm) ||
                     (operation.Section.Code != null && operation.Section.Code.ToLower().Contains(sectionTerm)))));
        }

        var transfers = await transfersQuery
            .OrderBy(x => x.Part != null ? x.Part.Name : string.Empty)
            .ThenBy(x => x.TransferDate)
            .ThenBy(x => x.Id)
            .ToListAsync(in_cancellationToken)
            .ConfigureAwait(false);

        var totalDays = (toDate - fromDate).Days;
        var dates = Enumerable.Range(0, totalDays + 1)
            .Select(offset => DateTime.SpecifyKind(fromDate.AddDays(offset), DateTimeKind.Unspecified))
            .ToList();

        var transferCells = transfers
            .Select(x => new TransferPeriodCell(
                x.PartId,
                x.Part != null ? x.Part.Name : "Деталь не указана",
                x.Part?.Code,
                ConvertToLocal(x.TransferDate).Date,
                BuildTransferCellText(x)))
            .ToList();

        var items = transferCells
            .GroupBy(x => new { x.PartId, x.PartName, x.PartCode })
            .OrderBy(group => group.Key.PartName)
            .Select(group =>
            {
                var cells = new Dictionary<DateTime, IReadOnlyList<string>>();

                foreach (var date in dates)
                {
                    var dayCells = group
                        .Where(x => x.Date == date)
                        .Select(x => x.Text)
                        .ToList();

                    cells[date] = dayCells.Count > 0 ? dayCells : Array.Empty<string>();
                }

                return new TransferPeriodReportItemViewModel(group.Key.PartName, group.Key.PartCode, cells);
            })
            .ToList();

        var filter = new TransferPeriodReportFilterViewModel
        {
            From = DateTime.SpecifyKind(fromDate, DateTimeKind.Unspecified),
            To = DateTime.SpecifyKind(toDate, DateTimeKind.Unspecified),
            Part = in_query?.Part,
            Section = in_query?.Section,
        };

        var ret = new TransferPeriodReportViewModel(filter, dates, items);
        return ret;
    }

    [HttpGet("wip-batches")]
    public async Task<IActionResult> WipBatchReport([FromQuery] WipBatchReportQuery? query, CancellationToken cancellationToken)
    {
        var now = DateTime.Now.Date;
        var defaultFrom = now.AddDays(-29);
        var (fromDate, toDate) = NormalizePeriod(query?.From ?? defaultFrom, query?.To ?? now);

        var fromUtc = ToUtcStartOfDay(fromDate);
        var toUtcExclusive = ToUtcStartOfDay(toDate.AddDays(1));

        var filter = new WipBatchReportFilterViewModel
        {
            From = DateTime.SpecifyKind(fromDate, DateTimeKind.Unspecified),
            To = DateTime.SpecifyKind(toDate, DateTimeKind.Unspecified),
            Part = query?.Part,
            Section = query?.Section,
            OpNumber = query?.OpNumber,
        };

        int? parsedOpNumber = null;
        if (!string.IsNullOrWhiteSpace(query?.OpNumber))
        {
            var trimmed = query.OpNumber.Trim();
            if (OperationNumber.TryParse(trimmed, out var opNumber))
            {
                parsedOpNumber = opNumber;
            }
            else
            {
                var emptyModel = new WipBatchReportViewModel(filter, Array.Empty<WipBatchReportItemViewModel>(), 0m);
                return View("~/Views/Reports/WipBatchReport.cshtml", emptyModel);
            }
        }

        var receiptInfoQuery = _dbContext.WipReceipts
            .AsNoTracking()
            .GroupBy(x => new { x.PartId, x.SectionId, x.OpNumber })
            .Select(g => new
            {
                g.Key.PartId,
                g.Key.SectionId,
                g.Key.OpNumber,
                LastReceiptDate = g.Max(x => x.ReceiptDate)
            })
            .Where(x => x.LastReceiptDate >= fromUtc && x.LastReceiptDate < toUtcExclusive);

        if (parsedOpNumber.HasValue)
        {
            receiptInfoQuery = receiptInfoQuery.Where(x => x.OpNumber == parsedOpNumber.Value);
        }

        var balancesQuery =
            from balance in _dbContext.WipBalances.AsNoTracking()
            join info in receiptInfoQuery
                on new { balance.PartId, balance.SectionId, balance.OpNumber }
                equals new { info.PartId, info.SectionId, info.OpNumber }
            join part in _dbContext.Parts.AsNoTracking()
                on balance.PartId equals part.Id
            join section in _dbContext.Sections.AsNoTracking()
                on balance.SectionId equals section.Id
            select new
            {
                PartName = part.Name,
                PartCode = part.Code,
                SectionName = section.Name,
                SectionCode = section.Code,
                balance.OpNumber,
                balance.Quantity,
                info.LastReceiptDate,
            };

        if (!string.IsNullOrWhiteSpace(query?.Part))
        {
            var term = query.Part.Trim().ToLowerInvariant();
            balancesQuery = balancesQuery.Where(x =>
                x.PartName.ToLower().Contains(term) ||
                (x.PartCode != null && x.PartCode.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(query?.Section))
        {
            var term = query.Section.Trim().ToLowerInvariant();
            balancesQuery = balancesQuery.Where(x =>
                x.SectionName.ToLower().Contains(term) ||
                (x.SectionCode != null && x.SectionCode.ToLower().Contains(term)));
        }

        var items = await balancesQuery
            .OrderByDescending(x => x.LastReceiptDate)
            .ThenBy(x => x.PartName)
            .ThenBy(x => x.OpNumber)
            .Select(x => new WipBatchReportItemViewModel(
                x.PartName,
                x.PartCode,
                x.SectionName,
                OperationNumber.Format(x.OpNumber),
                x.Quantity,
                ConvertToLocal(x.LastReceiptDate)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var model = new WipBatchReportViewModel(filter, items, items.Sum(x => x.Quantity));
        return View("~/Views/Reports/WipBatchReport.cshtml", model);
    }

    private async Task<(ScrapReportFilterViewModel Filter, List<ScrapReportItemViewModel> Items)> LoadScrapReportAsync(
        ScrapReportQuery? query,
        CancellationToken cancellationToken)
    {
        var now = DateTime.Now.Date;
        var defaultFrom = now.AddDays(-29);
        var (fromDate, toDate) = NormalizePeriod(query?.From ?? defaultFrom, query?.To ?? now);

        var fromUtc = ToUtcStartOfDay(fromDate);
        var toUtcExclusive = ToUtcStartOfDay(toDate.AddDays(1));

        IQueryable<WipScrap> scrapsQuery = _dbContext.WipScraps
            .AsNoTracking()
            .Where(x => x.RecordedAt >= fromUtc && x.RecordedAt < toUtcExclusive);

        if (!string.IsNullOrWhiteSpace(query?.Section))
        {
            var term = query.Section.Trim().ToLowerInvariant();
            scrapsQuery = scrapsQuery.Where(x =>
                x.Section != null &&
                (x.Section.Name.ToLower().Contains(term) ||
                 (x.Section.Code != null && x.Section.Code.ToLower().Contains(term))));
        }

        if (!string.IsNullOrWhiteSpace(query?.Part))
        {
            var term = query.Part.Trim().ToLowerInvariant();
            scrapsQuery = scrapsQuery.Where(x =>
                x.Part != null &&
                (x.Part.Name.ToLower().Contains(term) ||
                 (x.Part.Code != null && x.Part.Code.ToLower().Contains(term))));
        }

        if (!string.IsNullOrWhiteSpace(query?.ScrapType))
        {
            var scrapTypeText = query.ScrapType.Trim();
            if (Enum.TryParse<ScrapType>(scrapTypeText, true, out var scrapType))
            {
                scrapsQuery = scrapsQuery.Where(x => x.ScrapType == scrapType);
            }
        }

        var scraps = await scrapsQuery
            .Include(x => x.Part)
            .Include(x => x.Section)
            .Include(x => x.Transfer)
            .OrderByDescending(x => x.RecordedAt)
            .ThenBy(x => x.Part != null ? x.Part.Name : string.Empty)
            .ThenBy(x => x.OpNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(query?.Employee))
        {
            var employeeTerm = query.Employee.Trim();
            scraps = scraps
                .Where(x => ContainsUserId(x.UserId, employeeTerm) || (x.Transfer != null && ContainsUserId(x.Transfer.UserId, employeeTerm)))
                .ToList();
        }

        var items = scraps
            .Select(x => new ScrapReportItemViewModel(
                ConvertToLocal(x.RecordedAt),
                x.Section != null ? x.Section.Name : "Вид работ не задан",
                x.Part != null ? x.Part.Name : string.Empty,
                x.Part?.Code,
                OperationNumber.Format(x.OpNumber),
                x.Quantity,
                GetScrapTypeDisplayName(x.ScrapType),
                FormatEmployee(x.UserId),
                string.IsNullOrWhiteSpace(x.Comment) ? null : x.Comment))
            .ToList();

        var filter = new ScrapReportFilterViewModel
        {
            From = DateTime.SpecifyKind(fromDate, DateTimeKind.Unspecified),
            To = DateTime.SpecifyKind(toDate, DateTimeKind.Unspecified),
            Section = query?.Section,
            Part = query?.Part,
            ScrapType = query?.ScrapType,
            Employee = query?.Employee,
        };

        return (filter, items);
    }

    [HttpGet("wip-summary")]
    public async Task<IActionResult> WipSummary([FromQuery] WipSummaryQuery? query, CancellationToken cancellationToken)
    {
        var now = DateTime.Now.Date;
        var defaultFrom = now.AddDays(-29);
        var (fromDate, toDate) = NormalizePeriod(query?.From ?? defaultFrom, query?.To ?? now);

        var fromUtc = ToUtcStartOfDay(fromDate);
        var toUtcExclusive = ToUtcStartOfDay(toDate.AddDays(1));

        var receiptAggregates = await _dbContext.WipReceipts
            .AsNoTracking()
            .Where(x => x.ReceiptDate >= fromUtc && x.ReceiptDate < toUtcExclusive)
            .GroupBy(x => new { x.PartId, x.SectionId, x.OpNumber })
            .Select(g => new WipFlowAggregate(g.Key.PartId, g.Key.SectionId, g.Key.OpNumber, g.Sum(x => x.Quantity)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var launchAggregates = await _dbContext.WipLaunches
            .AsNoTracking()
            .Where(x => x.LaunchDate >= fromUtc && x.LaunchDate < toUtcExclusive)
            .GroupBy(x => new { x.PartId, x.SectionId, OpNumber = x.FromOpNumber })
            .Select(g => new WipFlowAggregate(g.Key.PartId, g.Key.SectionId, g.Key.OpNumber, g.Sum(x => x.Quantity)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var balances = await _dbContext.WipBalances
            .AsNoTracking()
            .Select(x => new WipFlowAggregate(x.PartId, x.SectionId, x.OpNumber, x.Quantity))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var partIds = new HashSet<Guid>(receiptAggregates.Select(x => x.PartId));
        partIds.UnionWith(launchAggregates.Select(x => x.PartId));
        partIds.UnionWith(balances.Select(x => x.PartId));

        var sectionIds = new HashSet<Guid>(receiptAggregates.Select(x => x.SectionId));
        sectionIds.UnionWith(launchAggregates.Select(x => x.SectionId));
        sectionIds.UnionWith(balances.Select(x => x.SectionId));

        var parts = await _dbContext.Parts
            .AsNoTracking()
            .Where(x => partIds.Contains(x.Id))
            .Select(x => new PartInfo(x.Id, x.Name, x.Code))
            .ToDictionaryAsync(x => x.Id, x => x, cancellationToken)
            .ConfigureAwait(false);

        var sections = await _dbContext.Sections
            .AsNoTracking()
            .Where(x => sectionIds.Contains(x.Id))
            .Select(x => new SectionInfo(x.Id, x.Name, x.Code))
            .ToDictionaryAsync(x => x.Id, x => x, cancellationToken)
            .ConfigureAwait(false);

        HashSet<Guid>? allowedPartIds = null;
        if (!string.IsNullOrWhiteSpace(query?.Part))
        {
            var term = query.Part.Trim().ToLowerInvariant();
            allowedPartIds = parts
                .Values
                .Where(x => x.Name.ToLower().Contains(term) || (x.Code != null && x.Code.ToLower().Contains(term)))
                .Select(x => x.Id)
                .ToHashSet();

            if (allowedPartIds.Count == 0)
            {
                var emptyModel = BuildWipSummaryModel(query, fromDate, toDate, Array.Empty<WipSummaryItemViewModel>());
                return View("~/Views/Reports/WipSummary.cshtml", emptyModel);
            }
        }

        HashSet<Guid>? allowedSectionIds = null;
        if (!string.IsNullOrWhiteSpace(query?.Section))
        {
            var term = query.Section.Trim().ToLowerInvariant();
            allowedSectionIds = sections
                .Values
                .Where(x => x.Name.ToLower().Contains(term) || (x.Code != null && x.Code.ToLower().Contains(term)))
                .Select(x => x.Id)
                .ToHashSet();

            if (allowedSectionIds.Count == 0)
            {
                var emptyModel = BuildWipSummaryModel(query, fromDate, toDate, Array.Empty<WipSummaryItemViewModel>());
                return View("~/Views/Reports/WipSummary.cshtml", emptyModel);
            }
        }

        var combined = new Dictionary<(Guid PartId, Guid SectionId, int OpNumber), WipSummaryItemBuilder>();

        foreach (var receipt in receiptAggregates)
        {
            if (allowedPartIds != null && !allowedPartIds.Contains(receipt.PartId))
            {
                continue;
            }

            if (allowedSectionIds != null && !allowedSectionIds.Contains(receipt.SectionId))
            {
                continue;
            }

            var builder = GetOrCreateBuilder(combined, receipt.PartId, receipt.SectionId, receipt.OpNumber, parts, sections);
            builder.Receipt += receipt.Quantity;
        }

        foreach (var launch in launchAggregates)
        {
            if (allowedPartIds != null && !allowedPartIds.Contains(launch.PartId))
            {
                continue;
            }

            if (allowedSectionIds != null && !allowedSectionIds.Contains(launch.SectionId))
            {
                continue;
            }

            var builder = GetOrCreateBuilder(combined, launch.PartId, launch.SectionId, launch.OpNumber, parts, sections);
            builder.Launch += launch.Quantity;
        }

        foreach (var balance in balances)
        {
            if (allowedPartIds != null && !allowedPartIds.Contains(balance.PartId))
            {
                continue;
            }

            if (allowedSectionIds != null && !allowedSectionIds.Contains(balance.SectionId))
            {
                continue;
            }

            var builder = GetOrCreateBuilder(combined, balance.PartId, balance.SectionId, balance.OpNumber, parts, sections);
            builder.Balance = balance.Quantity;
        }

        var items = combined.Values
            .OrderBy(x => x.PartName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.SectionName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.OpNumber)
            .Select(builder => new WipSummaryItemViewModel(
                builder.PartName,
                builder.PartCode,
                builder.SectionName,
                OperationNumber.Format(builder.OpNumber),
                builder.Receipt,
                builder.Launch,
                builder.Balance))
            .ToList();

        var model = BuildWipSummaryModel(query, fromDate, toDate, items);
        return View("~/Views/Reports/WipSummary.cshtml", model);
    }

    private WipSummaryViewModel BuildWipSummaryModel(WipSummaryQuery? query, DateTime fromDate, DateTime toDate, IReadOnlyList<WipSummaryItemViewModel> items)
    {
        var filter = new WipSummaryFilterViewModel
        {
            From = DateTime.SpecifyKind(fromDate, DateTimeKind.Unspecified),
            To = DateTime.SpecifyKind(toDate, DateTimeKind.Unspecified),
            Part = query?.Part,
            Section = query?.Section,
        };

        return new WipSummaryViewModel(
            filter,
            items,
            items.Sum(x => x.Receipt),
            items.Sum(x => x.Launch),
            items.Sum(x => x.Balance));
    }

    private static WipSummaryItemBuilder GetOrCreateBuilder(
        IDictionary<(Guid PartId, Guid SectionId, int OpNumber), WipSummaryItemBuilder> map,
        Guid partId,
        Guid sectionId,
        int opNumber,
        IReadOnlyDictionary<Guid, PartInfo> parts,
        IReadOnlyDictionary<Guid, SectionInfo> sections)
    {
        var key = (partId, sectionId, opNumber);
        if (!map.TryGetValue(key, out var builder))
        {
            parts.TryGetValue(partId, out var partInfo);
            sections.TryGetValue(sectionId, out var sectionInfo);

            builder = new WipSummaryItemBuilder
            {
                PartName = partInfo?.Name ?? "Неизвестная деталь",
                PartCode = partInfo?.Code,
                SectionName = sectionInfo?.Name ?? "Вид работ не задан",
                OpNumber = opNumber,
            };

            map[key] = builder;
        }

        return builder;
    }

    private static (DateTime From, DateTime To) NormalizePeriod(DateTime from, DateTime to)
    {
        var normalizedFrom = from.Date;
        var normalizedTo = to.Date;

        if (normalizedFrom > normalizedTo)
        {
            (normalizedFrom, normalizedTo) = (normalizedTo, normalizedFrom);
        }

        return (normalizedFrom, normalizedTo);
    }

    private static DateTime ToUtcStartOfDay(DateTime date)
    {
        var local = DateTime.SpecifyKind(date.Date, DateTimeKind.Local);
        return local.ToUniversalTime();
    }

    private static DateTime ConvertToLocal(DateTime value)
    {
        var utcValue = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        var local = utcValue.ToLocalTime();
        return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
    }

    private static string BuildTransferCellText(WipTransfer in_transfer)
    {
        var fromQuantity = in_transfer.Operations
            .Where(x => x.QuantityChange < 0m)
            .Sum(x => Math.Abs(x.QuantityChange));

        if (fromQuantity == 0m)
        {
            fromQuantity = Math.Abs(in_transfer.Quantity);
        }

        var toQuantity = in_transfer.Operations
            .Where(x => x.QuantityChange > 0m)
            .Sum(x => x.QuantityChange);

        if (toQuantity == 0m)
        {
            toQuantity = Math.Abs(in_transfer.Quantity);
        }

        var fromText = fromQuantity.ToString("0.###");
        var toText = toQuantity.ToString("0.###");

        var ret = string.Format(
            "{0} – {1} шт → {2} – {3} шт",
            OperationNumber.Format(in_transfer.FromOpNumber),
            fromText,
            OperationNumber.Format(in_transfer.ToOpNumber),
            toText);

        return ret;
    }

    public sealed record TransferPeriodReportQuery(
        DateTime? From,
        DateTime? To,
        string? Section,
        string? Part);

    public sealed record ReceiptReportQuery(
        DateTime? From,
        DateTime? To,
        string? Section,
        string? Part,
        decimal? MinQuantity,
        decimal? MaxQuantity);

    public sealed record ScrapReportQuery(
        DateTime? From,
        DateTime? To,
        string? Section,
        string? Part,
        string? ScrapType,
        string? Employee);

    public sealed record WipBatchReportQuery(
        DateTime? From,
        DateTime? To,
        string? Section,
        string? Part,
        string? OpNumber);

    public sealed record WipSummaryQuery(
        DateTime? From,
        DateTime? To,
        string? Section,
        string? Part);

    private sealed record WipFlowAggregate(Guid PartId, Guid SectionId, int OpNumber, decimal Quantity);

    private sealed record PartInfo(Guid Id, string Name, string? Code);

    private sealed record SectionInfo(Guid Id, string Name, string? Code);

    private sealed class WipSummaryItemBuilder
    {
        public string PartName { get; set; } = string.Empty;

        public string? PartCode { get; set; }

        public string SectionName { get; set; } = string.Empty;

        public int OpNumber { get; set; }

        public decimal Receipt { get; set; }

        public decimal Launch { get; set; }

        public decimal Balance { get; set; }
    }

    private sealed record TransferPeriodCell(Guid PartId, string PartName, string? PartCode, DateTime Date, string Text);

    private static string GetScrapTypeDisplayName(ScrapType scrapType)
        => scrapType switch
        {
            ScrapType.Technological => "Технологический",
            ScrapType.EmployeeFault => "По вине сотрудника",
            _ => scrapType.ToString(),
        };

    private static string FormatEmployee(Guid userId)
        => userId == Guid.Empty ? "Не указан" : userId.ToString();

    private static bool ContainsUserId(Guid userId, string term)
        => userId != Guid.Empty && userId.ToString().IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
}
