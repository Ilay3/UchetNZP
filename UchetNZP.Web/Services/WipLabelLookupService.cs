using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Infrastructure.Data;

namespace UchetNZP.Web.Services;

public interface IWipLabelLookupService
{
    Task<IReadOnlyDictionary<LabelLookupKey, IReadOnlyList<ReceiptLabelInfo>>> LoadAsync(
        IEnumerable<LabelLookupKey> in_keys,
        CancellationToken in_cancellationToken,
        DateTime? in_fromUtc = null,
        DateTime? in_toUtcExclusive = null);

    IReadOnlyList<string> FindLabelsUpToDate(
        IReadOnlyDictionary<LabelLookupKey, IReadOnlyList<ReceiptLabelInfo>> in_lookup,
        LabelLookupKey in_key,
        DateTime in_eventDate);

    IReadOnlyList<string> FindLabelsOnDate(
        IReadOnlyDictionary<LabelLookupKey, IReadOnlyList<ReceiptLabelInfo>> in_lookup,
        LabelLookupKey in_key,
        DateTime in_date);

    IReadOnlyList<string> FindLabelsInRange(
        IReadOnlyDictionary<LabelLookupKey, IReadOnlyList<ReceiptLabelInfo>> in_lookup,
        LabelLookupKey in_key,
        DateTime in_fromUtc,
        DateTime in_toUtcExclusive);

    IReadOnlyList<string> GetAllLabels(
        IReadOnlyDictionary<LabelLookupKey, IReadOnlyList<ReceiptLabelInfo>> in_lookup,
        LabelLookupKey in_key);
}

public sealed class WipLabelLookupService : IWipLabelLookupService
{
    private readonly AppDbContext m_dbContext;

    public WipLabelLookupService(AppDbContext in_dbContext)
    {
        m_dbContext = in_dbContext ?? throw new ArgumentNullException(nameof(in_dbContext));
    }

    public async Task<IReadOnlyDictionary<LabelLookupKey, IReadOnlyList<ReceiptLabelInfo>>> LoadAsync(
        IEnumerable<LabelLookupKey> in_keys,
        CancellationToken in_cancellationToken,
        DateTime? in_fromUtc = null,
        DateTime? in_toUtcExclusive = null)
    {
        if (in_keys is null)
        {
            throw new ArgumentNullException(nameof(in_keys));
        }

        var materialized = in_keys
            .Distinct()
            .ToList();

        var ret = new Dictionary<LabelLookupKey, IReadOnlyList<ReceiptLabelInfo>>();
        if (materialized.Count == 0)
        {
            return ret;
        }

        var partIds = materialized
            .Select(x => x.PartId)
            .Distinct()
            .ToList();

        var sectionIds = materialized
            .Select(x => x.SectionId)
            .Distinct()
            .ToList();

        var opNumbers = materialized
            .Select(x => x.OpNumber)
            .Distinct()
            .ToList();

        var query = m_dbContext.WipReceipts
            .AsNoTracking()
            .Where(x =>
                x.WipLabelId != null &&
                partIds.Contains(x.PartId) &&
                sectionIds.Contains(x.SectionId) &&
                opNumbers.Contains(x.OpNumber));

        if (in_fromUtc.HasValue)
        {
            var fromUtc = EnsureUtc(in_fromUtc.Value);
            query = query.Where(x => x.ReceiptDate >= fromUtc);
        }

        if (in_toUtcExclusive.HasValue)
        {
            var toUtc = EnsureUtc(in_toUtcExclusive.Value);
            query = query.Where(x => x.ReceiptDate < toUtc);
        }

        var receipts = await query
            .Select(x => new
            {
                x.PartId,
                x.SectionId,
                x.OpNumber,
                x.ReceiptDate,
                LabelNumber = x.WipLabel != null ? x.WipLabel.Number : null,
            })
            .ToListAsync(in_cancellationToken)
            .ConfigureAwait(false);

        foreach (var receipt in receipts)
        {
            if (string.IsNullOrWhiteSpace(receipt.LabelNumber))
            {
                continue;
            }

            var key = new LabelLookupKey(receipt.PartId, receipt.SectionId, receipt.OpNumber);
            if (!ret.TryGetValue(key, out var list))
            {
                list = new List<ReceiptLabelInfo>();
                ret[key] = list;
            }

            var normalizedNumber = receipt.LabelNumber!.Trim();
            var receiptDateUtc = EnsureUtc(receipt.ReceiptDate);
            ((List<ReceiptLabelInfo>)list).Add(new ReceiptLabelInfo(receiptDateUtc, normalizedNumber));
        }

        foreach (var pair in ret.ToList())
        {
            if (pair.Value.Count == 0)
            {
                continue;
            }

            var sorted = pair.Value
                .OrderByDescending(x => x.DateUtc)
                .ToList();

            ret[pair.Key] = sorted;
        }

        return ret;
    }

    public IReadOnlyList<string> FindLabelsUpToDate(
        IReadOnlyDictionary<LabelLookupKey, IReadOnlyList<ReceiptLabelInfo>> in_lookup,
        LabelLookupKey in_key,
        DateTime in_eventDate)
    {
        return ExtractLabels(in_lookup, in_key, info => info.DateUtc <= EnsureUtc(in_eventDate));
    }

    public IReadOnlyList<string> FindLabelsOnDate(
        IReadOnlyDictionary<LabelLookupKey, IReadOnlyList<ReceiptLabelInfo>> in_lookup,
        LabelLookupKey in_key,
        DateTime in_date)
    {
        var target = EnsureUtc(in_date);
        return ExtractLabels(in_lookup, in_key, info => info.DateUtc == target);
    }

    public IReadOnlyList<string> FindLabelsInRange(
        IReadOnlyDictionary<LabelLookupKey, IReadOnlyList<ReceiptLabelInfo>> in_lookup,
        LabelLookupKey in_key,
        DateTime in_fromUtc,
        DateTime in_toUtcExclusive)
    {
        var fromUtc = EnsureUtc(in_fromUtc);
        var toUtc = EnsureUtc(in_toUtcExclusive);
        return ExtractLabels(in_lookup, in_key, info => info.DateUtc >= fromUtc && info.DateUtc < toUtc);
    }

    public IReadOnlyList<string> GetAllLabels(
        IReadOnlyDictionary<LabelLookupKey, IReadOnlyList<ReceiptLabelInfo>> in_lookup,
        LabelLookupKey in_key)
    {
        return ExtractLabels(in_lookup, in_key, _ => true);
    }

    private static IReadOnlyList<string> ExtractLabels(
        IReadOnlyDictionary<LabelLookupKey, IReadOnlyList<ReceiptLabelInfo>> in_lookup,
        LabelLookupKey in_key,
        Func<ReceiptLabelInfo, bool> in_predicate)
    {
        if (in_lookup is null)
        {
            throw new ArgumentNullException(nameof(in_lookup));
        }

        if (!in_lookup.TryGetValue(in_key, out var labels) || labels.Count == 0)
        {
            return Array.Empty<string>();
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ret = new List<string>();

        foreach (var label in labels)
        {
            if (!in_predicate(label))
            {
                continue;
            }

            var normalized = label.Number.Trim();
            if (normalized.Length == 0)
            {
                continue;
            }

            if (seen.Add(normalized))
            {
                ret.Add(normalized);
            }
        }

        return ret;
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
}

public readonly record struct LabelLookupKey(Guid PartId, Guid SectionId, int OpNumber);

public sealed record ReceiptLabelInfo(DateTime DateUtc, string Number);
