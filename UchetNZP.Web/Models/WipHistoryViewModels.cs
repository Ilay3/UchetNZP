using System;
using System.Collections.Generic;
using System.Linq;
using UchetNZP.Shared;

namespace UchetNZP.Web.Models;

public enum WipHistoryEntryType
{
    Launch,
    Receipt,
    Transfer
}

public class WipHistoryFilterViewModel
{
    public DateTime From { get; init; }

    public DateTime To { get; init; }

    public IReadOnlyCollection<WipHistoryEntryType> Types { get; init; } = Array.Empty<WipHistoryEntryType>();

    public string PartSearch { get; init; } = string.Empty;

    public string SectionSearch { get; init; } = string.Empty;

    public bool IsTypeSelected(WipHistoryEntryType type)
    {
        return Types.Contains(type);
    }
}

public class WipHistoryOperationDetailViewModel
{
    public WipHistoryOperationDetailViewModel(
        string opNumber,
        string? operationName,
        string? sectionName,
        decimal? normHours,
        decimal? hours,
        decimal? quantityChange)
    {
        OpNumber = opNumber;
        OperationName = operationName ?? string.Empty;
        SectionName = sectionName ?? string.Empty;
        NormHours = normHours;
        Hours = hours;
        QuantityChange = quantityChange;
    }

    public string OpNumber { get; }

    public string OperationName { get; }

    public string SectionName { get; }

    public decimal? NormHours { get; }

    public decimal? Hours { get; }

    public decimal? QuantityChange { get; }

    public bool HasNormHours => NormHours.HasValue;

    public bool HasHours => Hours.HasValue;

    public bool HasQuantityChange => QuantityChange.HasValue && QuantityChange.Value != 0m;
}

public class WipHistoryLabelStepViewModel
{
    public WipHistoryLabelStepViewModel(
        Guid entryId,
        DateTime occurredAt,
        string typeDisplayName,
        string sectionName,
        string targetSectionName,
        string? operationRange,
        decimal quantity,
        bool isCancelled)
    {
        EntryId = entryId;
        OccurredAt = occurredAt;
        TypeDisplayName = typeDisplayName ?? string.Empty;
        SectionName = sectionName ?? string.Empty;
        TargetSectionName = targetSectionName ?? string.Empty;
        OperationRange = operationRange;
        Quantity = quantity;
        IsCancelled = isCancelled;
    }

    public Guid EntryId { get; }

    public DateTime OccurredAt { get; }

    public string TypeDisplayName { get; }

    public string SectionName { get; }

    public string TargetSectionName { get; }

    public string? OperationRange { get; }

    public decimal Quantity { get; }

    public bool IsCancelled { get; }
}

public class WipHistoryScrapViewModel
{
    public WipHistoryScrapViewModel(string type, decimal quantity, string? comment)
    {
        Type = type;
        Quantity = quantity;
        Comment = comment;
    }

    public string Type { get; }

    public decimal Quantity { get; }

    public string? Comment { get; }

    public bool HasComment => !string.IsNullOrWhiteSpace(Comment);
}

public class WipHistoryEntryViewModel
{
    public WipHistoryEntryViewModel(
        Guid id,
        WipHistoryEntryType type,
        DateTime occurredAt,
        string partName,
        string? partCode,
        string? sectionName,
        string? targetSectionName,
        string? fromOperation,
        string? toOperation,
        decimal quantity,
        decimal? hours,
        string? comment,
        string? labelNumber,
        IReadOnlyList<WipHistoryOperationDetailViewModel> operations,
        WipHistoryScrapViewModel? scrap,
        bool hasVersions,
        bool isReverted,
        Guid? versionId,
        Guid? auditId)
    {
        Id = id;
        Type = type;
        OccurredAt = DateTime.SpecifyKind(occurredAt, DateTimeKind.Unspecified);
        PartName = partName ?? string.Empty;
        PartCode = partCode;
        SectionName = sectionName ?? string.Empty;
        TargetSectionName = targetSectionName ?? string.Empty;
        FromOperation = fromOperation ?? string.Empty;
        ToOperation = toOperation ?? string.Empty;
        Quantity = quantity;
        Hours = hours;
        Comment = comment;
        LabelNumber = labelNumber;
        Operations = operations ?? Array.Empty<WipHistoryOperationDetailViewModel>();
        Scrap = scrap;
        HasVersions = hasVersions;
        IsReverted = isReverted;
        VersionId = versionId;
        AuditId = auditId;
    }

    public Guid Id { get; }

    public WipHistoryEntryType Type { get; }

    public DateTime OccurredAt { get; }

    public string PartName { get; }

    public string? PartCode { get; }

    public string SectionName { get; }

    public string TargetSectionName { get; }

    public string FromOperation { get; }

    public string ToOperation { get; }

    public decimal Quantity { get; }

    public decimal? Hours { get; }

    public string? Comment { get; }

    public string? LabelNumber { get; }

    public IReadOnlyList<WipHistoryOperationDetailViewModel> Operations { get; }

    public WipHistoryScrapViewModel? Scrap { get; }

    public bool HasVersions { get; }

    public bool IsReverted { get; }

    public Guid? VersionId { get; }

    public Guid? AuditId { get; }

    public bool CanDeleteReceipt { get; set; } = true;

    public IReadOnlyList<WipHistoryLabelStepViewModel> LabelTimeline { get; set; } = Array.Empty<WipHistoryLabelStepViewModel>();

    public DateTime Date => OccurredAt.Date;

    public string PartDisplayName => NameWithCodeFormatter.getNameWithCode(PartName, PartCode);

    public string TypeDisplayName => Type switch
    {
        WipHistoryEntryType.Launch => "Запуск",
        WipHistoryEntryType.Receipt => "Приход",
        WipHistoryEntryType.Transfer => "Передача",
        _ => "Операция"
    };

    public string? OperationRange
    {
        get
        {
            var hasFrom = !string.IsNullOrWhiteSpace(FromOperation);
            var hasTo = !string.IsNullOrWhiteSpace(ToOperation);

            if (!hasFrom && !hasTo)
            {
                return null;
            }

            if (!hasTo || string.Equals(FromOperation, ToOperation, StringComparison.Ordinal))
            {
                return hasFrom ? FromOperation : ToOperation;
            }

            return $"{FromOperation} → {ToOperation}";
        }
    }

    public bool HasHours => Hours.HasValue && Hours.Value != 0m;

    public bool HasComment => !string.IsNullOrWhiteSpace(Comment);

    public bool HasOperations => Operations.Count > 0;

    public bool HasScrap => Scrap is not null;

    public bool HasLabel => !string.IsNullOrWhiteSpace(LabelNumber);

    public bool HasTargetSection => !string.IsNullOrWhiteSpace(TargetSectionName);

    public bool IsCancelled => IsReverted;

    public string Status => IsCancelled ? "Отменено" : "Активно";

    public bool CanRevert => HasVersions && !IsReverted;
}

public class WipHistoryTypeSummaryViewModel
{
    public WipHistoryTypeSummaryViewModel(WipHistoryEntryType type, int count, decimal quantity)
    {
        Type = type;
        Count = count;
        Quantity = quantity;
    }

    public WipHistoryEntryType Type { get; }

    public int Count { get; }

    public decimal Quantity { get; }

    public string TypeDisplayName => Type switch
    {
        WipHistoryEntryType.Launch => "Запуски",
        WipHistoryEntryType.Receipt => "Приходы",
        WipHistoryEntryType.Transfer => "Передачи",
        _ => "Операции"
    };
}

public class WipHistoryDateGroupViewModel
{
    public WipHistoryDateGroupViewModel(
        DateTime date,
        IReadOnlyList<WipHistoryEntryViewModel> entries,
        IReadOnlyList<WipHistoryTypeSummaryViewModel> summaries)
    {
        Date = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
        Entries = entries ?? Array.Empty<WipHistoryEntryViewModel>();
        Summaries = summaries ?? Array.Empty<WipHistoryTypeSummaryViewModel>();
    }

    public DateTime Date { get; }

    public IReadOnlyList<WipHistoryEntryViewModel> Entries { get; }

    public IReadOnlyList<WipHistoryTypeSummaryViewModel> Summaries { get; }

    public int EntryCount => Entries.Count;

    public decimal TotalQuantity => Entries.Sum(x => x.Quantity);
}

public class WipHistoryViewModel
{
    public WipHistoryViewModel(WipHistoryFilterViewModel filter, IReadOnlyList<WipHistoryDateGroupViewModel> groups)
    {
        Filter = filter ?? throw new ArgumentNullException(nameof(filter));
        Groups = groups ?? throw new ArgumentNullException(nameof(groups));
    }

    public WipHistoryFilterViewModel Filter { get; }

    public IReadOnlyList<WipHistoryDateGroupViewModel> Groups { get; }

    public bool HasData => Groups.Count > 0;

    public int TotalEntries => Groups.Sum(x => x.EntryCount);

    public decimal TotalQuantity => Groups.Sum(x => x.TotalQuantity);
}

public record WipHistoryReceiptVersionViewModel(
    Guid VersionId,
    string Action,
    decimal? PreviousQuantity,
    decimal? NewQuantity,
    DateTime CreatedAt,
    string? Comment,
    string? LabelNumber,
    decimal PreviousBalance,
    decimal NewBalance);

public record WipHistoryReceiptVersionsViewModel(
    Guid ReceiptId,
    IReadOnlyList<WipHistoryReceiptVersionViewModel> Versions);

public class WipHistoryQuery
{
    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public string[]? Types { get; set; }

    public string? Part { get; set; }

    public string? Section { get; set; }
}
