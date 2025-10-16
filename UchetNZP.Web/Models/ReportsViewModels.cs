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
    int OpNumber,
    decimal Quantity,
    string? Comment);

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
    int OpNumber,
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
