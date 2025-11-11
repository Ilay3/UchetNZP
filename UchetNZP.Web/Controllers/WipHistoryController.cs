using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Shared;
using UchetNZP.Web.Models;

namespace UchetNZP.Web.Controllers;

[Route("wip/history")]
public class WipHistoryController : Controller
{
    private readonly AppDbContext _dbContext;

    public WipHistoryController(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
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
            selectedTypes = new HashSet<WipHistoryEntryType>
            {
                WipHistoryEntryType.Launch,
                WipHistoryEntryType.Receipt,
                WipHistoryEntryType.Transfer,
            };
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

            var receiptPartIds = receipts.Select(x => x.PartId).Distinct().ToList();
            var receiptSectionIds = receipts.Select(x => x.SectionId).Distinct().ToList();
            var opNumbers = receipts.Select(x => x.OpNumber).Distinct().ToList();

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
                    null);

                entries.Add(entry);
            }
        }

        if (selectedTypes.Contains(WipHistoryEntryType.Transfer) && canLoadData)
        {
            var fromUtc = ToUtcStartOfDay(fromDate);
            var toUtcExclusive = ToUtcStartOfDay(toDate.AddDays(1));

            var transfersQuery = _dbContext.WipTransfers
                .AsNoTracking()
                .Where(x => x.TransferDate >= fromUtc && x.TransferDate < toUtcExclusive)
                .Include(x => x.Part)
                .Include(x => x.Operations)
                    .ThenInclude(o => o.Operation)
                .Include(x => x.Operations)
                    .ThenInclude(o => o.Section)
                .Include(x => x.Scrap)
                .AsQueryable();

            if (hasPartFilter)
            {
                transfersQuery = transfersQuery.Where(x => partIds.Contains(x.PartId));
            }

            if (hasSectionFilter)
            {
                transfersQuery = transfersQuery.Where(x => sectionIds.Contains(x.FromSectionId) || sectionIds.Contains(x.ToSectionId));
            }

            var transfers = await transfersQuery
                .OrderBy(x => x.TransferDate)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var transferSectionIds = transfers
                .SelectMany(x => new[] { x.FromSectionId, x.ToSectionId })
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

            foreach (var transfer in transfers)
            {
                var orderedOperations = transfer.Operations
                    .OrderBy(op => op.OpNumber)
                    .Select(op => new WipHistoryOperationDetailViewModel(
                        OperationNumber.Format(op.OpNumber),
                        op.Operation != null ? op.Operation.Name : string.Empty,
                        op.Section != null ? op.Section.Name : string.Empty,
                        null,
                        null,
                        op.QuantityChange))
                    .ToList();

                var scrap = transfer.Scrap is null
                    ? null
                    : new WipHistoryScrapViewModel(
                        GetScrapDisplayName(transfer.Scrap.ScrapType),
                        transfer.Scrap.Quantity,
                        transfer.Scrap.Comment);

                sectionLookup.TryGetValue(transfer.FromSectionId, out var fromSectionName);
                sectionLookup.TryGetValue(transfer.ToSectionId, out var toSectionName);

                var entry = new WipHistoryEntryViewModel(
                    transfer.Id,
                    WipHistoryEntryType.Transfer,
                    ToLocalDateTime(transfer.TransferDate),
                    transfer.Part != null ? transfer.Part.Name : string.Empty,
                    transfer.Part?.Code,
                    fromSectionName ?? string.Empty,
                    toSectionName ?? string.Empty,
                    OperationNumber.Format(transfer.FromOpNumber),
                    OperationNumber.Format(transfer.ToOpNumber),
                    transfer.Quantity,
                    null,
                    transfer.Comment,
                    null,
                    orderedOperations,
                    scrap);

                entries.Add(entry);
            }
        }

        var pageSize = query?.PageSize ?? 25;
        if (pageSize < 1)
        {
            pageSize = 1;
        }
        else if (pageSize > 200)
        {
            pageSize = 200;
        }

        var orderedEntries = entries
            .OrderByDescending(x => x.OccurredAt.Date)
            .ThenBy(x => x.OccurredAt)
            .ThenBy(x => x.PartDisplayName, typeComparer)
            .ThenBy(x => x.Type)
            .ToList();

        var totalEntries = orderedEntries.Count;
        var totalQuantity = orderedEntries.Sum(x => x.Quantity);

        var totalPages = totalEntries == 0
            ? 1
            : (int)Math.Ceiling(totalEntries / (decimal)pageSize);

        var currentPage = query?.Page ?? 1;
        if (currentPage < 1)
        {
            currentPage = 1;
        }

        if (currentPage > totalPages)
        {
            currentPage = totalPages;
        }

        var pageEntries = orderedEntries
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var pageQuantity = pageEntries.Sum(x => x.Quantity);

        var grouped = pageEntries
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

        var model = new WipHistoryViewModel(
            filter,
            grouped,
            totalEntries,
            totalQuantity,
            pageEntries.Count,
            pageQuantity,
            currentPage,
            pageSize,
            totalPages);

        return View("~/Views/Wip/History.cshtml", model);
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
