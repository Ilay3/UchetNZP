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

    [HttpGet("label-movement")]
    public async Task<IActionResult> LabelMovementReport([FromQuery] LabelMovementReportQuery? query, CancellationToken cancellationToken)
    {
        var filter = new LabelMovementReportFilterViewModel
        {
            PartId = query?.PartId,
            LabelId = query?.LabelId,
        };

        if (query?.PartId is null || query.LabelId is null)
        {
            return View("~/Views/Reports/LabelMovementReport.cshtml", new LabelMovementReportViewModel(filter, Array.Empty<LabelMovementReportEventViewModel>(), 0m, 0m));
        }

        var part = await _dbContext.Parts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == query!.PartId!.Value, cancellationToken)
            .ConfigureAwait(false);

        var label = await _dbContext.WipLabels
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == query.LabelId!.Value && x.PartId == query.PartId!.Value, cancellationToken)
            .ConfigureAwait(false);

        if (part == null || label == null)
        {
            return View("~/Views/Reports/LabelMovementReport.cshtml", new LabelMovementReportViewModel(filter, Array.Empty<LabelMovementReportEventViewModel>(), 0m, 0m));
        }

        filter = new LabelMovementReportFilterViewModel
        {
            PartId = part.Id,
            PartName = part.Name,
            PartCode = part.Code,
            LabelId = label.Id,
            LabelNumber = label.Number,
        };

        var receipts = await _dbContext.WipReceipts
            .AsNoTracking()
            .Where(x => x.WipLabelId == label.Id)
            .Include(x => x.Section)
            .OrderBy(x => x.ReceiptDate)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var transferAudits = await _dbContext.TransferAudits
            .AsNoTracking()
            .Where(x => x.WipLabelId == label.Id)
            .OrderBy(x => x.TransferDate)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sectionIds = transferAudits
            .SelectMany(x => new[] { x.FromSectionId, x.ToSectionId })
            .Distinct()
            .ToList();

        var sectionLookup = sectionIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _dbContext.Sections
                .AsNoTracking()
                .Where(x => sectionIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken)
                .ConfigureAwait(false);

        var items = new List<LabelMovementReportEventViewModel>();

        items.AddRange(receipts.Select(receipt => new LabelMovementReportEventViewModel(
            ConvertToLocal(receipt.ReceiptDate),
            "Приход",
            "—",
            $"{receipt.Section?.Name ?? "Вид работ не задан"} • оп. {OperationNumber.Format(receipt.OpNumber)}",
            receipt.Quantity,
            null,
            null,
            receipt.Quantity,
            receipt.Comment)));

        items.AddRange(transferAudits.Select(audit =>
        {
            var fromSectionName = sectionLookup.TryGetValue(audit.FromSectionId, out var fromName)
                ? fromName
                : "Вид работ не задан";
            var toSectionName = sectionLookup.TryGetValue(audit.ToSectionId, out var toName)
                ? toName
                : "Вид работ не задан";

            return new LabelMovementReportEventViewModel(
                ConvertToLocal(audit.TransferDate),
                "Передача",
                $"{fromSectionName} • оп. {OperationNumber.Format(audit.FromOpNumber)}",
                $"{toSectionName} • оп. {OperationNumber.Format(audit.ToOpNumber)}",
                audit.Quantity,
                audit.ScrapQuantity > 0 ? audit.ScrapQuantity : null,
                audit.LabelQuantityBefore,
                audit.LabelQuantityAfter,
                audit.Comment);
        }));

        var orderedItems = items
            .OrderBy(x => x.Date)
            .ThenBy(x => x.EventType)
            .ToList();

        var model = new LabelMovementReportViewModel(
            filter,
            orderedItems,
            label.Quantity,
            label.RemainingQuantity);

        return View("~/Views/Reports/LabelMovementReport.cshtml", model);
    }

    [HttpGet("label-movement/parts")]
    public async Task<IActionResult> LabelMovementParts([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var query = _dbContext.Parts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(term) || (x.Code != null && x.Code.ToLower().Contains(term)));
        }

        var items = await query
            .OrderBy(x => x.Name)
            .Take(25)
            .Select(x => new LookupItemViewModel(x.Id, x.Name, x.Code))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(items);
    }

    [HttpGet("label-movement/labels")]
    public async Task<IActionResult> LabelMovementLabels([FromQuery] Guid partId, [FromQuery] string? search, CancellationToken cancellationToken)
    {
        if (partId == Guid.Empty)
        {
            return Ok(Array.Empty<object>());
        }

        var query = _dbContext.WipLabels
            .AsNoTracking()
            .Where(x => x.PartId == partId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Number.ToLower().Contains(term));
        }

        var items = await query
            .OrderByDescending(x => x.LabelDate)
            .ThenBy(x => x.Number)
            .Take(100)
            .Select(x => new
            {
                id = x.Id,
                number = x.Number,
                quantity = x.Quantity,
                remainingQuantity = x.RemainingQuantity,
                labelDate = x.LabelDate,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(items);
    }

    private async Task<WipBatchReportViewModel> LoadWipBatchReportAsync(
        WipBatchReportQuery? query,
        CancellationToken cancellationToken)
    {
        var now = DateTime.Now.Date;
        var defaultFrom = now.AddDays(-6);
        var (fromDate, toDate) = NormalizePeriod(query?.From ?? defaultFrom, query?.To ?? now);


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

        var receiptLabels = await _dbContext.WipReceipts
            .AsNoTracking()
            .Where(x => x.WipLabelId != null)
            .GroupBy(x => new
            {
                x.PartId,
                x.SectionId,
                x.OpNumber,
                LabelId = x.WipLabelId!.Value,
            })
            .Select(g => new
            {
                g.Key.PartId,
                g.Key.SectionId,
                g.Key.OpNumber,
                LabelId = g.Key.LabelId,
                Quantity = g.Sum(x => x.Quantity),
                LastReceiptDate = g.Max(x => x.ReceiptDate),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var transferLabels = await _dbContext.TransferAudits
            .AsNoTracking()
            .Where(x => !x.IsReverted && x.WipLabelId != null)
            .Select(x => new
            {
                x.PartId,
                x.FromSectionId,
                x.FromOpNumber,
                x.ToSectionId,
                x.ToOpNumber,
                x.Quantity,
                x.ScrapQuantity,
                x.IsWarehouseTransfer,
                x.TransferDate,
                LabelId = x.WipLabelId!.Value,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var keyBalances = new Dictionary<(Guid PartId, Guid SectionId, int OpNumber, Guid LabelId), (decimal Quantity, DateTime LastDate)>();

        foreach (var item in receiptLabels)
        {
            var key = (item.PartId, item.SectionId, item.OpNumber, item.LabelId);
            if (keyBalances.TryGetValue(key, out var existing))
            {
                keyBalances[key] = (existing.Quantity + item.Quantity, existing.LastDate > item.LastReceiptDate ? existing.LastDate : item.LastReceiptDate);
            }
            else
            {
                keyBalances[key] = (item.Quantity, item.LastReceiptDate);
            }
        }

        foreach (var transfer in transferLabels)
        {
            var fromKey = (transfer.PartId, transfer.FromSectionId, transfer.FromOpNumber, transfer.LabelId);
            var fromDelta = transfer.Quantity + transfer.ScrapQuantity;
            if (keyBalances.TryGetValue(fromKey, out var fromExisting))
            {
                keyBalances[fromKey] = (fromExisting.Quantity - fromDelta, fromExisting.LastDate > transfer.TransferDate ? fromExisting.LastDate : transfer.TransferDate);
            }
            else
            {
                keyBalances[fromKey] = (-fromDelta, transfer.TransferDate);
            }

            if (!transfer.IsWarehouseTransfer)
            {
                var toKey = (transfer.PartId, transfer.ToSectionId, transfer.ToOpNumber, transfer.LabelId);
                if (keyBalances.TryGetValue(toKey, out var toExisting))
                {
                    keyBalances[toKey] = (toExisting.Quantity + transfer.Quantity, toExisting.LastDate > transfer.TransferDate ? toExisting.LastDate : transfer.TransferDate);
                }
                else
                {
                    keyBalances[toKey] = (transfer.Quantity, transfer.TransferDate);
                }
            }
        }

        var positiveLabelBalances = keyBalances
            .Where(x => x.Value.Quantity > 0m)
            .Select(x => new
            {
                x.Key.PartId,
                x.Key.SectionId,
                x.Key.OpNumber,
                x.Key.LabelId,
                Quantity = x.Value.Quantity,
                LastDate = x.Value.LastDate,
            })
            .ToList();

        var partIds = positiveLabelBalances.Select(x => x.PartId).Distinct().ToList();
        var sectionIds = positiveLabelBalances.Select(x => x.SectionId).Distinct().ToList();
        var labelIds = positiveLabelBalances.Select(x => x.LabelId).Distinct().ToList();

        var partLookup = partIds.Count == 0
            ? new Dictionary<Guid, (string Name, string? Code)>()
            : await _dbContext.Parts.AsNoTracking()
                .Where(x => partIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => (x.Name, x.Code), cancellationToken)
                .ConfigureAwait(false);

        var sectionLookup = sectionIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _dbContext.Sections.AsNoTracking()
                .Where(x => sectionIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken)
                .ConfigureAwait(false);

        var labelLookup = labelIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _dbContext.WipLabels.AsNoTracking()
                .Where(x => labelIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Number ?? string.Empty, cancellationToken)
                .ConfigureAwait(false);

        var groupedItems = positiveLabelBalances
            .GroupBy(x => new { x.PartId, x.SectionId, x.OpNumber })
            .Select(group =>
            {
                partLookup.TryGetValue(group.Key.PartId, out var part);
                sectionLookup.TryGetValue(group.Key.SectionId, out var sectionName);

                var labels = group
                    .Select(x =>
                    {
                        var number = labelLookup.TryGetValue(x.LabelId, out var labelNumber) ? labelNumber : null;
                        var normalized = string.IsNullOrWhiteSpace(number) ? "Без номера" : number!.Trim();
                        return $"{normalized}: {x.Quantity:0.###}";
                    })
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new WipBatchReportItemViewModel(
                    part.Name,
                    part.Code,
                    sectionName ?? string.Empty,
                    OperationNumber.Format(group.Key.OpNumber),
                    group.Sum(x => x.Quantity),
                    ConvertToLocal(group.Max(x => x.LastDate)),
                    labels.Count == 0 ? null : string.Join(", ", labels));
            })
            .Where(x => x.Quantity > 0m)
            .ToList();

        if (!string.IsNullOrWhiteSpace(query?.Part))
        {
            var term = query.Part.Trim().ToLowerInvariant();
            groupedItems = groupedItems
                .Where(x =>
                    x.PartName.ToLower().Contains(term) ||
                    (!string.IsNullOrWhiteSpace(x.PartCode) && x.PartCode!.ToLower().Contains(term)))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(query?.Section))
        {
            var term = query.Section.Trim().ToLowerInvariant();
            groupedItems = groupedItems
                .Where(x => x.SectionName.ToLower().Contains(term))
                .ToList();
        }

        if (parsedOpNumber.HasValue)
        {
            var formatted = OperationNumber.Format(parsedOpNumber.Value);
            groupedItems = groupedItems.Where(x => string.Equals(x.OpNumber, formatted, StringComparison.Ordinal)).ToList();
        }

        var items = groupedItems
            .OrderByDescending(x => x.BatchDate)
            .ThenBy(x => x.PartName)
            .ThenBy(x => x.OpNumber)
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

    public sealed record LabelMovementReportQuery(
        Guid? PartId,
        Guid? LabelId);

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
