using System;
using System.Collections.Generic;

namespace UchetNZP.Web.Models;

public class ReceiptReportFilterViewModel
{
    public DateTime From { get; init; }

    public DateTime To { get; init; }

    public string? Section { get; init; }

    public string? Part { get; init; }

    public decimal? MinQuantity { get; init; }

    public decimal? MaxQuantity { get; init; }
}

public record ReceiptReportItemViewModel(
    DateTime Date,
    string SectionName,
    string PartName,
    string? PartCode,
    string OpNumber,
    decimal Quantity,
    string? Comment,
    string? LabelNumber);

public record ReceiptReportViewModel(
    ReceiptReportFilterViewModel Filter,
    IReadOnlyList<ReceiptReportItemViewModel> Items,
    decimal TotalQuantity)
{
    public bool HasData => Items.Count > 0;
}

public class WipSummaryFilterViewModel
{
    public DateTime From { get; init; }

    public DateTime To { get; init; }

    public string? Part { get; init; }

    public string? Section { get; init; }
}

public record WipSummaryItemViewModel(
    string PartName,
    string? PartCode,
    string SectionName,
    string OpNumber,
    decimal Receipt,
    decimal Launch,
    decimal Balance);

public record WipSummaryViewModel(
    WipSummaryFilterViewModel Filter,
    IReadOnlyList<WipSummaryItemViewModel> Items,
    decimal TotalReceipt,
    decimal TotalLaunch,
    decimal TotalBalance)
{
    public bool HasData => Items.Count > 0;
}

public class ScrapReportFilterViewModel
{
    public DateTime From { get; init; }

    public DateTime To { get; init; }

    public string? Section { get; init; }

    public string? Part { get; init; }

    public string? ScrapType { get; init; }

    public string? Employee { get; init; }
}

public record ScrapReportItemViewModel(
    DateTime Date,
    string SectionName,
    string PartName,
    string? PartCode,
    string OpNumber,
    decimal Quantity,
    string ScrapType,
    string Employee,
    string? Comment);

public record ScrapReportViewModel(
    ScrapReportFilterViewModel Filter,
    IReadOnlyList<ScrapReportItemViewModel> Items,
    decimal TotalQuantity)
{
    public bool HasData => Items.Count > 0;
}

public class WipBatchReportFilterViewModel
{
    public DateTime From { get; init; }

    public DateTime To { get; init; }

    public string? Part { get; init; }

    public string? Section { get; init; }

    public string? OpNumber { get; init; }
}

public record WipBatchReportItemViewModel(
    string PartName,
    string? PartCode,
    string SectionName,
    string OpNumber,
    decimal Quantity,
    DateTime BatchDate);

public record WipBatchReportViewModel(
    WipBatchReportFilterViewModel Filter,
    IReadOnlyList<WipBatchReportItemViewModel> Items,
    decimal TotalQuantity)
{
    public bool HasData => Items.Count > 0;
}

public class TransferPeriodReportFilterViewModel
{
    public DateTime From { get; init; }

    public DateTime To { get; init; }

    public string? Part { get; init; }

    public string? Section { get; init; }
}

public record TransferPeriodReportItemViewModel(
    string PartName,
    string? PartCode,
    IReadOnlyDictionary<DateTime, IReadOnlyList<string>> Cells);

public record TransferPeriodReportViewModel(
    TransferPeriodReportFilterViewModel Filter,
    IReadOnlyList<DateTime> Dates,
    IReadOnlyList<TransferPeriodReportItemViewModel> Items)
{
    public bool HasData => Items.Count > 0;
}
