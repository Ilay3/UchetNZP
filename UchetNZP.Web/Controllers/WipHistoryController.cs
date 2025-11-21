using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Shared;
using UchetNZP.Web.Models;

namespace UchetNZP.Web.Controllers;

[Route("wip/history")]
public class WipHistoryController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly IWipService? _wipService;

    public WipHistoryController(AppDbContext dbContext, IWipService? wipService = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _wipService = wipService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] WipHistoryQuery? query, CancellationToken cancellationToken)
    {
        var now = DateTime.Now.Date;
        var defaultFrom = now.AddDays(-13);
        var (fromDate, toDate) = NormalizePeriod(query?.From ?? defaultFrom, query?.To ?? now);

        var selectedTypes = ParseTypes(query?.Types);
        if (selectedTypes.Count == 0)
        {
            selectedTypes = GetDefaultTypes();
        }

        var partSearch = query?.Part;
        if (string.IsNullOrWhiteSpace(partSearch))
        {
            partSearch = string.Empty;
        }
        else
        {
            partSearch = partSearch.Trim();
        }

        var sectionSearch = query?.Section;
        if (string.IsNullOrWhiteSpace(sectionSearch))
        {
            sectionSearch = string.Empty;
        }
        else
        {
            sectionSearch = sectionSearch.Trim();
        }

        var hasPartFilter = partSearch.Length > 0;
        var hasSectionFilter = sectionSearch.Length > 0;

        var partIds = new List<Guid>();
        if (hasPartFilter)
        {
            var normalizedPart = partSearch.ToLowerInvariant();
            partIds = await _dbContext.Parts
                .AsNoTracking()
                .Where(part =>
                    part.Name.ToLower().IndexOf(normalizedPart) >= 0 ||
                    (part.Code != null && part.Code.ToLower().IndexOf(normalizedPart) >= 0))
                .Select(part => part.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var sectionIds = new List<Guid>();
        if (hasSectionFilter)
        {
            var normalizedSection = sectionSearch.ToLowerInvariant();
            sectionIds = await _dbContext.Sections
                .AsNoTracking()
                .Where(section =>
                    section.Name.ToLower().IndexOf(normalizedSection) >= 0 ||
                    (section.Code != null && section.Code.ToLower().IndexOf(normalizedSection) >= 0))
                .Select(section => section.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var canLoadData = (!hasPartFilter || partIds.Count > 0) && (!hasSectionFilter || sectionIds.Count > 0);

        var entries = new List<WipHistoryEntryViewModel>();
        var typeComparer = StringComparer.CurrentCultureIgnoreCase;

        if (selectedTypes.Contains(WipHistoryEntryType.Launch) && canLoadData)
        {
            var fromUtc = ToUtcStartOfDay(fromDate);
            var toUtcExclusive = ToUtcStartOfDay(toDate.AddDays(1));

            var launchesQuery = _dbContext.WipLaunches
                .AsNoTracking()
                .Where(x => x.LaunchDate >= fromUtc && x.LaunchDate < toUtcExclusive)
                .Include(x => x.Part)
                .Include(x => x.Section)
                .Include(x => x.Operations)
                    .ThenInclude(o => o.Operation)
                .Include(x => x.Operations)
                    .ThenInclude(o => o.Section)
                .AsQueryable();

            if (hasPartFilter)
            {
                launchesQuery = launchesQuery.Where(x => partIds.Contains(x.PartId));
            }

            if (hasSectionFilter)
            {
                launchesQuery = launchesQuery.Where(x => sectionIds.Contains(x.SectionId));
            }

            var launches = await launchesQuery
                .OrderBy(x => x.LaunchDate)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var launch in launches)
            {
                var orderedOperations = launch.Operations
                    .OrderBy(op => op.OpNumber)
                    .Select(op => new WipHistoryOperationDetailViewModel(
                        OperationNumber.Format(op.OpNumber),
                        op.Operation != null ? op.Operation.Name : string.Empty,
                        op.Section != null ? op.Section.Name : string.Empty,
                        op.NormHours,
                        op.Hours,
                        null))
                    .ToList();

                var lastOperationNumber = orderedOperations.Count > 0
                    ? orderedOperations[^1].OpNumber
                    : OperationNumber.Format(launch.FromOpNumber);

                var entry = new WipHistoryEntryViewModel(
                    launch.Id,
                    WipHistoryEntryType.Launch,
                    ToLocalDateTime(launch.LaunchDate),
                    launch.Part != null ? launch.Part.Name : string.Empty,
                    launch.Part?.Code,
                    launch.Section != null ? launch.Section.Name : string.Empty,
                    string.Empty,
                    OperationNumber.Format(launch.FromOpNumber),
                    lastOperationNumber,
                    launch.Quantity,
                    launch.SumHoursToFinish,
                    launch.Comment,
                    null,
                    orderedOperations,
                    null,
                    false,
                    false,
                    null,
                    null);

                entries.Add(entry);
            }
        }

        if (selectedTypes.Contains(WipHistoryEntryType.Receipt) && canLoadData)
        {
            var fromUtc = ToUtcStartOfDay(fromDate);
            var toUtcExclusive = ToUtcStartOfDay(toDate.AddDays(1));

            var receiptsQuery = _dbContext.WipReceipts
                .AsNoTracking()
                .Where(x => x.ReceiptDate >= fromUtc && x.ReceiptDate < toUtcExclusive)
                .Include(x => x.Part)
                .Include(x => x.Section)
                .Include(x => x.WipLabel)
                .AsQueryable();

            if (hasPartFilter)
            {
                receiptsQuery = receiptsQuery.Where(x => partIds.Contains(x.PartId));
            }

            if (hasSectionFilter)
            {
                receiptsQuery = receiptsQuery.Where(x => sectionIds.Contains(x.SectionId));
            }

            var receipts = await receiptsQuery
                .OrderBy(x => x.ReceiptDate)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var receiptIds = receipts.Select(x => x.Id).Distinct().ToList();
            var receiptPartIds = receipts.Select(x => x.PartId).Distinct().ToList();
            var receiptSectionIds = receipts.Select(x => x.SectionId).Distinct().ToList();
            var opNumbers = receipts.Select(x => x.OpNumber).Distinct().ToList();

            var receiptAudits = receipts.Count == 0
                ? new List<ReceiptAudit>()
                : await _dbContext.ReceiptAudits
                    .AsNoTracking()
                    .Where(audit => receiptIds.Contains(audit.ReceiptId))
                    .OrderByDescending(audit => audit.CreatedAt)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

            var receiptAuditLookup = receiptAudits
                .GroupBy(audit => audit.ReceiptId)
                .ToDictionary(group => group.Key, group => group.ToList());

            var routes = await _dbContext.PartRoutes
                .AsNoTracking()
                .Where(route =>
                    receiptPartIds.Contains(route.PartId) &&
                    receiptSectionIds.Contains(route.SectionId) &&
                    opNumbers.Contains(route.OpNumber))
                .Include(route => route.Operation)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var routeLookup = routes.ToDictionary(
                route => (route.PartId, route.SectionId, route.OpNumber),
                route => route);

            foreach (var receipt in receipts)
            {
                routeLookup.TryGetValue((receipt.PartId, receipt.SectionId, receipt.OpNumber), out var route);
                receiptAuditLookup.TryGetValue(receipt.Id, out var audits);
                var hasVersions = audits is { Count: > 0 };
                Guid? latestVersionId = hasVersions ? audits![0].VersionId : null;

                var entry = new WipHistoryEntryViewModel(
                    receipt.Id,
                    WipHistoryEntryType.Receipt,
                    ToLocalDateTime(receipt.ReceiptDate),
                    receipt.Part != null ? receipt.Part.Name : string.Empty,
                    receipt.Part?.Code,
                    receipt.Section != null ? receipt.Section.Name : string.Empty,
                    string.Empty,
                    OperationNumber.Format(receipt.OpNumber),
                    OperationNumber.Format(receipt.OpNumber),
                    receipt.Quantity,
                    null,
                    receipt.Comment,
                    receipt.WipLabel != null && !string.IsNullOrWhiteSpace(receipt.WipLabel.Number)
                        ? receipt.WipLabel.Number
                        : null,
                    route != null
                        ? new List<WipHistoryOperationDetailViewModel>
                        {
                            new WipHistoryOperationDetailViewModel(
                                OperationNumber.Format(route.OpNumber),
                                route.Operation != null ? route.Operation.Name : string.Empty,
                                receipt.Section != null ? receipt.Section.Name : string.Empty,
                                route.NormHours,
                                null,
                                null)
                        }
                        : Array.Empty<WipHistoryOperationDetailViewModel>(),
                    null,
                    hasVersions,
                    false,
                    latestVersionId,
                    null);

                entries.Add(entry);
            }
        }

        if (selectedTypes.Contains(WipHistoryEntryType.Transfer) && canLoadData)
        {
            var fromUtc = ToUtcStartOfDay(fromDate);
            var toUtcExclusive = ToUtcStartOfDay(toDate.AddDays(1));

            var transferAuditsQuery = _dbContext.TransferAudits
                .AsNoTracking()
                .Where(x => x.TransferDate >= fromUtc && x.TransferDate < toUtcExclusive)
                .Include(x => x.Operations)
                .AsQueryable();

            if (hasPartFilter)
            {
                transferAuditsQuery = transferAuditsQuery.Where(x => partIds.Contains(x.PartId));
            }

            if (hasSectionFilter)
            {
                transferAuditsQuery = transferAuditsQuery.Where(x => sectionIds.Contains(x.FromSectionId) || sectionIds.Contains(x.ToSectionId));
            }

            var wipTransfersQuery = _dbContext.WipTransfers
                .AsNoTracking()
                .Where(x => x.TransferDate >= fromUtc && x.TransferDate < toUtcExclusive)
                .Include(x => x.Operations)
                .Include(x => x.Scrap)
                .Include(x => x.WipLabel)
                .AsQueryable();

            if (hasPartFilter)
            {
                wipTransfersQuery = wipTransfersQuery.Where(x => partIds.Contains(x.PartId));
            }

            if (hasSectionFilter)
            {
                wipTransfersQuery = wipTransfersQuery.Where(x =>
                    sectionIds.Contains(x.FromSectionId) ||
                    sectionIds.Contains(x.ToSectionId) ||
                    x.Operations.Any(operation => sectionIds.Contains(operation.SectionId)));
            }

            var transferAudits = await transferAuditsQuery
                .OrderBy(x => x.TransferDate)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var wipTransfers = await wipTransfersQuery
                .OrderBy(x => x.TransferDate)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var transferSectionIds = transferAudits
                .SelectMany(x => new[] { x.FromSectionId, x.ToSectionId }.Concat(x.Operations.Select(o => o.SectionId)))
                .Concat(wipTransfers.SelectMany(x => new[] { x.FromSectionId, x.ToSectionId }.Concat(x.Operations.Select(o => o.SectionId))))
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            var sectionLookup = transferSectionIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await _dbContext.Sections
                    .AsNoTracking()
                    .Where(section => transferSectionIds.Contains(section.Id))
                    .ToDictionaryAsync(section => section.Id, section => section.Name, cancellationToken)
                    .ConfigureAwait(false);

            var transferPartIds = transferAudits
                .Select(x => x.PartId)
                .Concat(wipTransfers.Select(x => x.PartId))
                .Distinct()
                .ToList();

            var transferPartLookup = transferPartIds.Count == 0
                ? new Dictionary<Guid, (string Name, string? Code)>()
                : await _dbContext.Parts
                    .AsNoTracking()
                    .Where(part => transferPartIds.Contains(part.Id))
                    .ToDictionaryAsync(part => part.Id, part => (part.Name, part.Code), cancellationToken)
                    .ConfigureAwait(false);

            var operationIds = transferAudits
                .SelectMany(x => x.Operations)
                .Select(x => x.OperationId)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Concat(wipTransfers.SelectMany(x => x.Operations).Select(x => x.OperationId))
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            var operationLookup = operationIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await _dbContext.Operations
                    .AsNoTracking()
                    .Where(operation => operationIds.Contains(operation.Id))
                    .ToDictionaryAsync(operation => operation.Id, operation => operation.Name, cancellationToken)
                    .ConfigureAwait(false);

            foreach (var transfer in transferAudits)
            {
                var orderedOperations = transfer.Operations
                    .OrderBy(op => op.OpNumber)
                    .Select(op =>
                    {
                        operationLookup.TryGetValue(op.OperationId ?? Guid.Empty, out var operationName);
                        sectionLookup.TryGetValue(op.SectionId, out var opSectionName);

                        return new WipHistoryOperationDetailViewModel(
                            OperationNumber.Format(op.OpNumber),
                            operationName ?? string.Empty,
                            opSectionName ?? string.Empty,
                            null,
                            null,
                            op.QuantityChange);
                    })
                    .ToList();

                var scrap = transfer.ScrapType is null || transfer.ScrapQuantity <= 0m
                    ? null
                    : new WipHistoryScrapViewModel(
                        GetScrapDisplayName(transfer.ScrapType.Value),
                        transfer.ScrapQuantity,
                        transfer.ScrapComment);

                sectionLookup.TryGetValue(transfer.FromSectionId, out var fromSectionName);
                sectionLookup.TryGetValue(transfer.ToSectionId, out var toSectionName);

                transferPartLookup.TryGetValue(transfer.PartId, out var partInfo);

                var entry = new WipHistoryEntryViewModel(
                    transfer.TransferId,
                    WipHistoryEntryType.Transfer,
                    ToLocalDateTime(transfer.TransferDate),
                    partInfo.Name ?? string.Empty,
                    partInfo.Code,
                    fromSectionName ?? string.Empty,
                    toSectionName ?? string.Empty,
                    OperationNumber.Format(transfer.FromOpNumber),
                    OperationNumber.Format(transfer.ToOpNumber),
                    transfer.Quantity,
                    null,
                    transfer.Comment,
                    transfer.LabelNumber,
                    orderedOperations,
                    scrap,
                    true,
                    transfer.IsReverted,
                    null,
                    transfer.Id);

                entries.Add(entry);
            }

            foreach (var transfer in wipTransfers)
            {
                var orderedOperations = transfer.Operations
                    .OrderBy(op => op.OpNumber)
                    .Select(op =>
                    {
                        operationLookup.TryGetValue(op.OperationId, out var operationName);
                        sectionLookup.TryGetValue(op.SectionId, out var opSectionName);

                        return new WipHistoryOperationDetailViewModel(
                            OperationNumber.Format(op.OpNumber),
                            operationName ?? string.Empty,
                            opSectionName ?? string.Empty,
                            null,
                            null,
                            op.QuantityChange);
                    })
                    .ToList();

                var scrap = transfer.Scrap is null || transfer.Scrap.Quantity <= 0m
                    ? null
                    : new WipHistoryScrapViewModel(
                        GetScrapDisplayName(transfer.Scrap.ScrapType),
                        transfer.Scrap.Quantity,
                        transfer.Scrap.Comment);

                sectionLookup.TryGetValue(transfer.FromSectionId, out var fromSection);
                sectionLookup.TryGetValue(transfer.ToSectionId, out var toSection);

                transferPartLookup.TryGetValue(transfer.PartId, out var partInfo);

                var labelNumber = transfer.WipLabel is not null && !string.IsNullOrWhiteSpace(transfer.WipLabel.Number)
                    ? transfer.WipLabel.Number
                    : null;

                var entry = new WipHistoryEntryViewModel(
                    transfer.Id,
                    WipHistoryEntryType.Transfer,
                    ToLocalDateTime(transfer.TransferDate),
                    partInfo.Name ?? string.Empty,
                    partInfo.Code,
                    fromSection ?? string.Empty,
                    toSection ?? string.Empty,
                    OperationNumber.Format(transfer.FromOpNumber),
                    OperationNumber.Format(transfer.ToOpNumber),
                    transfer.Quantity,
                    null,
                    transfer.Comment,
                    labelNumber,
                    orderedOperations,
                    scrap,
                    false,
                    false,
                    null,
                    null);

                entries.Add(entry);
            }
        }

        var deduplicatedEntries = entries
    .GroupBy(entry => (entry.Type, entry.Id))
    .Select(group => group
        .OrderByDescending(HasActionButtons)
        .ThenByDescending(entry => entry.HasVersions)
        .ThenByDescending(entry => entry.OccurredAt)
        .First())
    .ToList();

        var grouped = deduplicatedEntries
            .GroupBy(x => x.Date)
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var items = g
                    .OrderBy(x => x.OccurredAt)
                    .ThenBy(x => x.PartDisplayName, typeComparer)
                    .ToList();

                var summaries = items
                    .GroupBy(x => x.Type)
                    .Select(x => new WipHistoryTypeSummaryViewModel(
                        x.Key,
                        x.Count(),
                        x.Sum(item => item.Quantity)))
                    .OrderBy(x => x.Type)
                    .ToList();

                return new WipHistoryDateGroupViewModel(
                    DateTime.SpecifyKind(g.Key, DateTimeKind.Unspecified),
                    items,
                    summaries);
            })
            .ToList();

        var filter = new WipHistoryFilterViewModel
        {
            From = DateTime.SpecifyKind(fromDate, DateTimeKind.Unspecified),
            To = DateTime.SpecifyKind(toDate, DateTimeKind.Unspecified),
            Types = selectedTypes
                .OrderBy(x => x)
                .ToList(),
            PartSearch = partSearch,
            SectionSearch = sectionSearch,
        };

        var model = new WipHistoryViewModel(filter, grouped);

        return View("~/Views/Wip/History.cshtml", model);
    }

    private static bool HasActionButtons(WipHistoryEntryViewModel entry)
    {
        return entry.Type == WipHistoryEntryType.Receipt ||
            (entry.Type == WipHistoryEntryType.Transfer && entry.HasVersions && entry.AuditId.HasValue);
    }


    private static HashSet<WipHistoryEntryType> ParseTypes(string[]? types)
    {
        var result = new HashSet<WipHistoryEntryType>();
        if (types is null || types.Length == 0)
        {
            return result;
        }

        foreach (var value in types)
        {
            if (TryParseType(value, out var parsed))
            {
                result.Add(parsed);
            }
        }

        return result;
    }

    private static HashSet<WipHistoryEntryType> GetDefaultTypes()
    {
        return Enum.GetValues<WipHistoryEntryType>()
            .ToHashSet();
    }

    [HttpDelete("receipt/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteReceipt(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest("Некорректный идентификатор прихода.");
        }

        if (_wipService is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, "Сервис обработки НЗП недоступен.");
        }

        try
        {
            var result = await _wipService.DeleteReceiptAsync(id, cancellationToken).ConfigureAwait(false);

            var viewModel = new ReceiptDeleteResultViewModel(
                result.ReceiptId,
                result.BalanceId,
                result.PartId,
                result.SectionId,
                OperationNumber.Format(result.OpNumber),
                result.ReceiptQuantity,
                result.PreviousQuantity,
                result.RestoredQuantity,
                result.Delta,
                result.VersionId);

            return Ok(viewModel);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private static bool TryParseType(string? value, out WipHistoryEntryType type)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "launch":
            case "launches":
                type = WipHistoryEntryType.Launch;
                return true;
            case "receipt":
            case "receipts":
                type = WipHistoryEntryType.Receipt;
                return true;
            case "transfer":
            case "transfers":
                type = WipHistoryEntryType.Transfer;
                return true;
            default:
                type = default;
                return false;
        }
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

    private static DateTime ToLocalDateTime(DateTime value)
    {
        var utcValue = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        var local = utcValue.ToLocalTime();
        return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
    }

    private static string GetScrapDisplayName(ScrapType type)
    {
        return type switch
        {
            ScrapType.Technological => "Технологический брак",
            ScrapType.EmployeeFault => "Брак по вине сотрудника",
            _ => "Брак",
        };
    }
}
