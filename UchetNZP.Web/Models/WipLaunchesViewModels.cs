using System;
using System.Collections.Generic;
using System.Linq;

namespace UchetNZP.Web.Models;

public class LaunchHistoryFilterViewModel
{
    public DateTime From { get; init; }

    public DateTime To { get; init; }
}

public class LaunchHistoryOperationViewModel
{
    public LaunchHistoryOperationViewModel(string opNumber, string operationName, string sectionName, decimal normHours, decimal hours)
    {
        OpNumber = opNumber;
        OperationName = operationName ?? string.Empty;
        SectionName = sectionName ?? string.Empty;
        NormHours = normHours;
        Hours = hours;
    }

    public string OpNumber { get; }

    public string OperationName { get; }

    public string SectionName { get; }

    public decimal NormHours { get; }

    public decimal Hours { get; }
}

public class LaunchHistoryItemViewModel
{
    public LaunchHistoryItemViewModel(
        Guid id,
        DateTime launchDate,
        string partName,
        string? partCode,
        string sectionName,
        string fromOperation,
        decimal quantity,
        decimal hours,
        string? comment,
        IReadOnlyList<LaunchHistoryOperationViewModel> operations)
    {
        Id = id;
        LaunchDate = launchDate;
        PartName = partName ?? string.Empty;
        PartCode = partCode;
        SectionName = sectionName ?? string.Empty;
        FromOperation = fromOperation;
        Quantity = quantity;
        Hours = hours;
        Comment = comment;
        Operations = operations ?? Array.Empty<LaunchHistoryOperationViewModel>();
    }

    public Guid Id { get; }

    public DateTime LaunchDate { get; }

    public string PartName { get; }

    public string? PartCode { get; }

    public string SectionName { get; }

    public string FromOperation { get; }

    public decimal Quantity { get; }

    public decimal Hours { get; }

    public string? Comment { get; }

    public IReadOnlyList<LaunchHistoryOperationViewModel> Operations { get; }

    public DateTime Date => LaunchDate.Date;

    public string PartDisplayName => string.IsNullOrWhiteSpace(PartCode)
        ? PartName
        : $"{PartName} ({PartCode})";

    public string OperationRange
    {
        get
        {
            if (Operations.Count == 0)
            {
                return FromOperation;
            }

            var lastOp = Operations[^1].OpNumber;
            if (lastOp == FromOperation)
            {
                return FromOperation;
            }

            return $"{FromOperation} → {lastOp}";
        }
    }

    public bool HasComment => !string.IsNullOrWhiteSpace(Comment);
}

public class LaunchHistoryDateGroupViewModel
{
    public LaunchHistoryDateGroupViewModel(
        DateTime date,
        int launchCount,
        decimal quantity,
        decimal hours,
        IReadOnlyList<LaunchHistoryItemViewModel> launches,
        IReadOnlyList<LaunchHistorySectionSummaryViewModel> sectionSummaries)
    {
        Date = date;
        LaunchCount = launchCount;
        Quantity = quantity;
        Hours = hours;
        Launches = launches ?? Array.Empty<LaunchHistoryItemViewModel>();
        SectionSummaries = sectionSummaries ?? Array.Empty<LaunchHistorySectionSummaryViewModel>();
    }

    public DateTime Date { get; }

    public int LaunchCount { get; }

    public decimal Quantity { get; }

    public decimal Hours { get; }

    public IReadOnlyList<LaunchHistoryItemViewModel> Launches { get; }

    public IReadOnlyList<LaunchHistorySectionSummaryViewModel> SectionSummaries { get; }
}

public class LaunchHistorySectionSummaryViewModel
{
    public LaunchHistorySectionSummaryViewModel(string sectionName, decimal hours)
    {
        SectionName = string.IsNullOrWhiteSpace(sectionName) ? "Не указан" : sectionName;
        Hours = hours;
    }

    public string SectionName { get; }

    public decimal Hours { get; }
}

public class LaunchHistoryViewModel
{
    public LaunchHistoryViewModel(
        LaunchHistoryFilterViewModel filter,
        IReadOnlyList<LaunchHistoryDateGroupViewModel> dates)
    {
        Filter = filter ?? throw new ArgumentNullException(nameof(filter));
        Dates = dates ?? throw new ArgumentNullException(nameof(dates));
    }

    public LaunchHistoryFilterViewModel Filter { get; }

    public IReadOnlyList<LaunchHistoryDateGroupViewModel> Dates { get; }

    public int TotalLaunchCount => Dates.Sum(x => x.LaunchCount);

    public decimal TotalQuantity => Dates.Sum(x => x.Quantity);

    public decimal TotalHours => Dates.Sum(x => x.Hours);

    public bool HasData => Dates.Count > 0;
}

public class LaunchHistoryQuery
{
    public DateTime? From { get; set; }

    public DateTime? To { get; set; }
}

public record LaunchDeleteResponseModel(
    Guid LaunchId,
    Guid PartId,
    Guid SectionId,
    string FromOperation,
    decimal Remaining,
    string Message
);
