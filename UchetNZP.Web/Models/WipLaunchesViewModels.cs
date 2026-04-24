using System;
using System.Collections.Generic;
using System.Linq;
using UchetNZP.Shared;

namespace UchetNZP.Web.Models;

public class LaunchHistoryFilterViewModel
{
    public DateTime From { get; init; }

    public DateTime To { get; init; }

    public Guid? MetalMaterialId { get; init; }

    public IReadOnlyList<LookupItemViewModel> MaterialOptions { get; init; } = Array.Empty<LookupItemViewModel>();
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
        IReadOnlyList<LaunchHistoryOperationViewModel> operations,
        IReadOnlyList<LaunchMetalNeedItemViewModel> metalNeeds,
        LaunchMetalRequirementShortViewModel? metalRequirement)
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
        MetalNeeds = metalNeeds ?? Array.Empty<LaunchMetalNeedItemViewModel>();
        MetalRequirement = metalRequirement;
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

    public IReadOnlyList<LaunchMetalNeedItemViewModel> MetalNeeds { get; }

    public LaunchMetalRequirementShortViewModel? MetalRequirement { get; }

    public bool HasMetalNeeds => MetalNeeds.Count > 0;

    public bool HasMetalRequirement => MetalRequirement is not null;

    public DateTime Date => LaunchDate.Date;

    public string PartDisplayName => NameWithCodeFormatter.getNameWithCode(PartName, PartCode);

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

public class LaunchMetalNeedItemViewModel
{
    public LaunchMetalNeedItemViewModel(
        Guid metalMaterialId,
        string materialName,
        string? materialCode,
        decimal normPerUnit,
        string? sizeRaw,
        decimal quantity,
        decimal totalRequiredQty,
        string unit,
        decimal? weightPerUnitKg,
        decimal coefficient,
        decimal totalRequiredWeightKg,
        decimal stockQty,
        decimal stockWeightKg)
    {
        MetalMaterialId = metalMaterialId;
        MaterialName = materialName ?? string.Empty;
        MaterialCode = materialCode;
        NormPerUnit = normPerUnit;
        SizeRaw = sizeRaw;
        Quantity = quantity;
        TotalRequiredQty = totalRequiredQty;
        Unit = unit ?? string.Empty;
        WeightPerUnitKg = weightPerUnitKg;
        Coefficient = coefficient;
        TotalRequiredWeightKg = totalRequiredWeightKg;
        StockQty = stockQty;
        StockWeightKg = stockWeightKg;
    }

    public Guid MetalMaterialId { get; }

    public string MaterialName { get; }

    public string? MaterialCode { get; }

    public decimal NormPerUnit { get; }

    public string? SizeRaw { get; }

    public decimal Quantity { get; }

    public decimal TotalRequiredQty { get; }

    public string Unit { get; }

    public decimal? WeightPerUnitKg { get; }

    public decimal Coefficient { get; }

    public decimal TotalRequiredWeightKg { get; }

    public decimal StockQty { get; }

    public decimal StockWeightKg { get; }

    public decimal DifferenceQty => StockQty - TotalRequiredQty;

    public bool IsEnough => StockWeightKg >= TotalRequiredWeightKg;

    public decimal DifferenceWeightKg => StockWeightKg - TotalRequiredWeightKg;

    public string MaterialDisplayName => string.IsNullOrWhiteSpace(MaterialCode) ? MaterialName : $"{MaterialName} ({MaterialCode})";
}

public class LaunchMetalRequirementShortViewModel
{
    public LaunchMetalRequirementShortViewModel(Guid id, string number, DateTime date, string status)
    {
        Id = id;
        Number = number ?? string.Empty;
        Date = date;
        Status = status ?? string.Empty;
    }

    public Guid Id { get; }

    public string Number { get; }

    public DateTime Date { get; }

    public string Status { get; }
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

    public Guid? MetalMaterialId { get; set; }
}

public record LaunchDeleteResponseModel(
    Guid LaunchId,
    Guid PartId,
    Guid SectionId,
    string FromOperation,
    decimal Remaining,
    string Message
);

public record MaterialStockSummaryViewModel(
    string UnitOfMeasure,
    string AvailableInStock,
    int UnitsCount,
    decimal TotalSize,
    decimal TotalWeightKg
);

public record MaterialStockUnitItemViewModel(
    string Code,
    decimal Size,
    string UnitOfMeasure,
    decimal WeightKg,
    DateTime ReceiptDate
);

public record MaterialStockResponseViewModel(
    MaterialStockSummaryViewModel? Summary = null,
    IReadOnlyList<MaterialStockUnitItemViewModel>? Units = null
)
{
    public MaterialStockSummaryViewModel? Summary { get; init; } = Summary;

    public IReadOnlyList<MaterialStockUnitItemViewModel> Units { get; init; } = Units ?? Array.Empty<MaterialStockUnitItemViewModel>();
}
