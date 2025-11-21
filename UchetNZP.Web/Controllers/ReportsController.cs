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
    private readonly IWipBatchReportExcelExporter _wipBatchReportExcelExporter;
    private readonly IWipLabelLookupService _labelLookupService;

    public ReportsController(
        AppDbContext dbContext,
        IScrapReportExcelExporter scrapReportExcelExporter,
        ITransferPeriodReportExcelExporter transferPeriodReportExcelExporter,
        IWipBatchReportExcelExporter wipBatchReportExcelExporter,
        IWipLabelLookupService labelLookupService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _scrapReportExcelExporter = scrapReportExcelExporter ?? throw new ArgumentNullException(nameof(scrapReportExcelExporter));
        _transferPeriodReportExcelExporter = transferPeriodReportExcelExporter ?? throw new ArgumentNullException(nameof(transferPeriodReportExcelExporter));
        _wipBatchReportExcelExporter = wipBatchReportExcelExporter ?? throw new ArgumentNullException(nameof(wipBatchReportExcelExporter));
        _labelLookupService = labelLookupService ?? throw new ArgumentNullException(nameof(labelLookupService));
    }

    [HttpGet("receipts")]
    public async Task<IActionResult> ReceiptReport([FromQuery] ReceiptReportQuery? query, CancellationToken cancellationToken)
    {
        var now = DateTime.Now.Date;
        var defaultFrom = now.AddDays(-6);
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
        var defaultFrom = now.AddDays(-6);
        var period = NormalizePeriod(in_query?.From ?? defaultFrom, in_query?.To ?? now);
        var fromDate = period.From;
        var toDate = period.To;

        var fromUtc = ToUtcStartOfDay(fromDate);
        var toUtcExclusive = ToUtcStartOfDay(toDate.AddDays(1));

        IQueryable<WipTransfer> transfersQuery = _dbContext.WipTransfers
            .AsNoTracking()
            .Where(x => x.TransferDate >= fromUtc && x.TransferDate < toUtcExclusive)
            .Include(x => x.Part)
            .Include(x => x.WipLabel)
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

        var transferLabelKeys = transfers
            .Select(x => new LabelLookupKey(x.PartId, x.FromSectionId, x.FromOpNumber))
            .ToList();

        var maxTransferDateUtc = transfers.Count > 0
            ? EnsureUtc(transfers.Max(x => x.TransferDate)).AddDays(1)
            : (DateTime?)null;

        var transferLabelLookup = await _labelLookupService.LoadAsync(
                transferLabelKeys,
                in_cancellationToken,
                null,
                maxTransferDateUtc)
            .ConfigureAwait(false);

        var totalDays = (toDate - fromDate).Days;
        var dates = Enumerable.Range(0, totalDays + 1)
            .Select(offset => DateTime.SpecifyKind(fromDate.AddDays(offset), DateTimeKind.Unspecified))
            .ToList();

        var transferCells = transfers
            .Select(x =>
            {
                var labelNumbers = _labelLookupService.FindLabelsUpToDate(
                    transferLabelLookup,
                    new LabelLookupKey(x.PartId, x.FromSectionId, x.FromOpNumber),
                    x.TransferDate);

                return new TransferPeriodCell(
                    x.PartId,
                    x.Part != null ? x.Part.Name : "Деталь не указана",
                    x.Part?.Code,
                    ConvertToLocal(x.TransferDate).Date,
                    BuildTransferCellText(x, labelNumbers));
            })
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
        var model = await LoadWipBatchReportAsync(query, cancellationToken).ConfigureAwait(false);
        return View("~/Views/Reports/WipBatchReport.cshtml", model);
    }

    [HttpGet("wip-batches/export")]
    public async Task<IActionResult> WipBatchReportExport([FromQuery] WipBatchReportQuery? query, CancellationToken cancellationToken)
    {
        var model = await LoadWipBatchReportAsync(query, cancellationToken).ConfigureAwait(false);
        var content = _wipBatchReportExcelExporter.Export(model.Filter, model.Items, model.TotalQuantity);
        var fileName = string.Format(
            "wip-batch-report-{0:yyyyMMdd}-{1:yyyyMMdd}.xlsx",
            model.Filter.From,
            model.Filter.To);
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private async Task<WipBatchReportViewModel> LoadWipBatchReportAsync(
        WipBatchReportQuery? query,
        CancellationToken cancellationToken)
    {
        var now = DateTime.Now.Date;
        var defaultFrom = now.AddDays(-6);
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
                return emptyModel;
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
            where balance.Quantity > 0
            select new
            {
                balance.PartId,
                balance.SectionId,
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

        var rawBatchItems = await balancesQuery
            .Where(x => x.Quantity > 0)
            .OrderByDescending(x => x.LastReceiptDate)
            .ThenBy(x => x.PartName)
            .ThenBy(x => x.OpNumber)
            .Select(x => new
            {
                x.PartId,
                x.SectionId,
                x.PartName,
                x.PartCode,
                x.SectionName,
                x.OpNumber,
                x.Quantity,
                x.LastReceiptDate,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var batchLabelKeys = rawBatchItems
            .Select(x => new LabelLookupKey(x.PartId, x.SectionId, x.OpNumber))
            .ToList();

        var maxBatchDateUtc = rawBatchItems.Count > 0
            ? EnsureUtc(rawBatchItems.Max(x => x.LastReceiptDate)).AddDays(1)
            : (DateTime?)null;

        var batchLabelLookup = await _labelLookupService.LoadAsync(
                batchLabelKeys,
                cancellationToken,
                null,
                maxBatchDateUtc)
            .ConfigureAwait(false);

        var items = rawBatchItems
            .Select(x =>
            {
                var labels = _labelLookupService.FindLabelsOnDate(
                    batchLabelLookup,
                    new LabelLookupKey(x.PartId, x.SectionId, x.OpNumber),
                    x.LastReceiptDate);
                var labelText = labels.Count > 0 ? string.Join(", ", labels) : null;

                return new WipBatchReportItemViewModel(
                    x.PartName,
                    x.PartCode,
                    x.SectionName,
                    OperationNumber.Format(x.OpNumber),
                    x.Quantity,
                    ConvertToLocal(x.LastReceiptDate),
                    labelText);
            })
            .ToList();

        var model = new WipBatchReportViewModel(filter, items, items.Sum(x => x.Quantity));
        return model;
    }

    private async Task<(ScrapReportFilterViewModel Filter, List<ScrapReportItemViewModel> Items)> LoadScrapReportAsync(
        ScrapReportQuery? query,
        CancellationToken cancellationToken)
    {
        var now = DateTime.Now.Date;
        var defaultFrom = now.AddDays(-6);
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

        var partIds = scraps
            .Select(x => x.PartId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var opNumbers = scraps
            .Select(x => x.OpNumber)
            .Distinct()
            .ToList();

        var routeLookup = await _dbContext.PartRoutes
            .AsNoTracking()
            .Include(x => x.Section)
            .Include(x => x.Operation)
            .Where(x => partIds.Contains(x.PartId) && opNumbers.Contains(x.OpNumber))
            .GroupBy(x => new { x.PartId, x.OpNumber })
            .ToDictionaryAsync(
                x => (x.Key.PartId, x.Key.OpNumber),
                x => new ScrapRouteInfo(
                    x.Select(route => route.Operation?.Name).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)),
                    x.Select(route => route.Section?.Name).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))),
                cancellationToken)
            .ConfigureAwait(false);

        var scrapLabelKeys = scraps
            .Select(x => new LabelLookupKey(x.PartId, x.SectionId, x.OpNumber))
            .ToList();

        var maxScrapDateUtc = scraps.Count > 0
            ? EnsureUtc(scraps.Max(x => x.RecordedAt)).AddDays(1)
            : (DateTime?)null;

        var scrapLabelLookup = await _labelLookupService.LoadAsync(
                scrapLabelKeys,
                cancellationToken,
                null,
                maxScrapDateUtc)
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
                GetSectionName(x, routeLookup),
                x.Part != null ? x.Part.Name : string.Empty,
                x.Part?.Code,
                OperationNumber.Format(x.OpNumber),
                GetOperationName(x, routeLookup),
                x.Quantity,
                GetScrapTypeDisplayName(x.ScrapType),
                FormatEmployee(x.UserId),
                string.IsNullOrWhiteSpace(x.Comment) ? null : x.Comment,
                FormatLabelList(_labelLookupService.FindLabelsUpToDate(
                    scrapLabelLookup,
                    new LabelLookupKey(x.PartId, x.SectionId, x.OpNumber),
                    x.RecordedAt))))
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

    private static string? FormatLabelList(IReadOnlyList<string> in_labels)
    {
        if (in_labels is null || in_labels.Count == 0)
        {
            return null;
        }

        return string.Join(", ", in_labels);
    }

    private static DateTime EnsureUtc(DateTime in_value)
    {
        return in_value.Kind switch
        {
            DateTimeKind.Utc => in_value,
            DateTimeKind.Local => in_value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(in_value, DateTimeKind.Utc),
        };
    }

    private static string BuildTransferCellText(WipTransfer in_transfer, IReadOnlyList<string> in_labelNumbers)
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

        var actualLabelNumbers = new List<string>();

        if (in_transfer.WipLabel is not null && !string.IsNullOrWhiteSpace(in_transfer.WipLabel.Number))
        {
            var labelText = in_transfer.WipLabel.Number;
            if (!string.IsNullOrWhiteSpace(in_transfer.Comment))
            {
                labelText = string.Concat(labelText, " (", in_transfer.Comment, ")");
            }

            actualLabelNumbers.Add(labelText);
        }
        else if (in_labelNumbers is not null && in_labelNumbers.Count > 0)
        {
            actualLabelNumbers.AddRange(in_labelNumbers);
        }

        if (actualLabelNumbers.Count > 0)
        {
            var prefix = actualLabelNumbers.Count == 1 ? "Ярлык" : "Ярлыки";
            ret = string.Concat(ret, Environment.NewLine, prefix, ": ", string.Join(", ", actualLabelNumbers));
        }
        else if (!string.IsNullOrWhiteSpace(in_transfer.Comment))
        {
            ret = string.Concat(ret, Environment.NewLine, "Комментарий: ", in_transfer.Comment);
        }

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

    private static string GetOperationName(
        WipScrap in_scrap,
        IReadOnlyDictionary<(Guid PartId, int OpNumber), ScrapRouteInfo> in_routeLookup)
    {
        var ret = GetSectionName(in_scrap, in_routeLookup);

        if (in_routeLookup.TryGetValue((in_scrap.PartId, in_scrap.OpNumber), out var routeInfo) &&
            !string.IsNullOrWhiteSpace(routeInfo.OperationName))
        {
            ret = routeInfo.OperationName!;
        }

        return ret;
    }

    private static string GetSectionName(
        WipScrap in_scrap,
        IReadOnlyDictionary<(Guid PartId, int OpNumber), ScrapRouteInfo> in_routeLookup)
    {
        var ret = in_scrap.Section != null ? in_scrap.Section.Name : "Вид работ не задан";

        if (in_routeLookup.TryGetValue((in_scrap.PartId, in_scrap.OpNumber), out var routeInfo) &&
            !string.IsNullOrWhiteSpace(routeInfo.SectionName))
        {
            ret = routeInfo.SectionName!;
        }

        return ret;
    }

    private sealed record ScrapRouteInfo(string? OperationName, string? SectionName);
}
