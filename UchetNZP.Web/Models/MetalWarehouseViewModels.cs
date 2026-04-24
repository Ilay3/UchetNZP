using System.Globalization;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace UchetNZP.Web.Models;

public class MetalWarehouseDashboardViewModel
{
    public int MaterialsInCatalog { get; init; }

    public decimal MetalUnitsInStock { get; init; }

    public int OpenRequirements { get; init; }

    public int MovementsToday { get; init; }

    public bool HasAnyData => MaterialsInCatalog > 0 || MetalUnitsInStock > 0 || OpenRequirements > 0 || MovementsToday > 0;

    public string MetalUnitsInStockDisplay => MetalUnitsInStock.ToString("0.###", CultureInfo.CurrentCulture);
}

public class MetalWarehouseListPageViewModel
{
    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public IReadOnlyCollection<string> Headers { get; init; } = Array.Empty<string>();

    public IReadOnlyCollection<IReadOnlyCollection<string>> Rows { get; init; } = Array.Empty<IReadOnlyCollection<string>>();

    public string EmptyStateTitle { get; init; } = "Пока пусто";

    public string EmptyStateDescription { get; init; } = "Данные появятся после запуска следующих этапов модуля.";
}

public class MetalStockFilterViewModel
{
    public Guid? MaterialId { get; init; }

    public string? UnitCodeOrNumber { get; init; }

    public string? UnitOfMeasure { get; init; }

    public bool ActiveOnly { get; init; }

    public IReadOnlyCollection<SelectListItem> Materials { get; init; } = Array.Empty<SelectListItem>();

    public IReadOnlyCollection<SelectListItem> UnitOfMeasures { get; init; } = Array.Empty<SelectListItem>();
}

public class MetalStockItemViewModel
{
    public Guid Id { get; init; }

    public string GeneratedCode { get; init; } = string.Empty;

    public string MaterialName { get; init; } = string.Empty;

    public decimal SizeValue { get; init; }

    public string SizeUnitText { get; init; } = string.Empty;

    public decimal WeightKg { get; init; }

    public string ReceiptNumber { get; init; } = string.Empty;

    public DateTime ReceiptDate { get; init; }

    public string BatchNumber { get; init; } = string.Empty;

    public string StockCategory { get; init; } = string.Empty;

    public string Status { get; init; } = "В наличии";
}

public class MetalStockPageViewModel
{
    public MetalStockFilterViewModel Filters { get; init; } = new();

    public IReadOnlyCollection<MetalStockItemViewModel> Items { get; init; } = Array.Empty<MetalStockItemViewModel>();

    public int TotalUnitsCount { get; init; }

    public int TotalMaterialsCount { get; init; }

    public decimal TotalWeightKg { get; init; }

    public decimal TotalSize { get; init; }

    public bool HasItems => Items.Count > 0;
}

public class MetalStockItemHistoryEntryViewModel
{
    public DateTime Timestamp { get; init; }

    public string EventName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
}

public class MetalStockItemDetailsViewModel
{
    public Guid Id { get; init; }

    public string GeneratedCode { get; init; } = string.Empty;

    public string MaterialName { get; init; } = string.Empty;

    public decimal SizeValue { get; init; }

    public string SizeUnitText { get; init; } = string.Empty;

    public decimal WeightKg { get; init; }

    public string ReceiptNumber { get; init; } = string.Empty;

    public DateTime ReceiptDate { get; init; }

    public string Source { get; init; } = string.Empty;

    public string? ReceiptComment { get; init; }

    public string Status { get; init; } = "В наличии";

    public IReadOnlyCollection<MetalStockItemHistoryEntryViewModel> History { get; init; } = Array.Empty<MetalStockItemHistoryEntryViewModel>();
}

public class MetalReceiptListItemViewModel
{
    public Guid Id { get; init; }

    public string ReceiptNumber { get; init; } = string.Empty;

    public DateTime ReceiptDate { get; init; }

    public string SupplierOrSource { get; init; } = string.Empty;

    public int PositionsCount { get; init; }
}

public class MetalReceiptListViewModel
{
    public IReadOnlyCollection<MetalReceiptListItemViewModel> Receipts { get; init; } = Array.Empty<MetalReceiptListItemViewModel>();
}

public class MetalReceiptUnitInputViewModel
{
    public int ItemIndex { get; set; }

    public decimal? SizeValue { get; set; }
}

public class MetalReceiptCreateViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Дата прихода обязательна.")]
    [DataType(DataType.Date)]
    public DateTime? ReceiptDate { get; set; }

    [StringLength(256, ErrorMessage = "Источник не должен превышать 256 символов.")]
    public string? SupplierOrSource { get; set; }

    [Required(ErrorMessage = "Материал обязателен.")]
    public Guid? MetalMaterialId { get; set; }

    [Range(0.000001d, 999999999999d, ErrorMessage = "Вес должен быть больше 0.")]
    public decimal? PassportWeightKg { get; set; }

    public decimal? TotalWeightKg
    {
        get => PassportWeightKg;
        set => PassportWeightKg = value;
    }

    [Range(0.000001d, 999999999999d, ErrorMessage = "Фактический вес должен быть больше 0.")]
    public decimal? ActualWeightKg { get; set; }

    [Range(1, 9999, ErrorMessage = "Количество должно быть больше 0.")]
    public int? Quantity { get; set; }

    [StringLength(256, ErrorMessage = "Комментарий не должен превышать 256 символов.")]
    public string? Comment { get; set; }

    public List<MetalReceiptUnitInputViewModel> Units { get; set; } = new();

    [StringLength(32, ErrorMessage = "Номер партии не должен превышать 32 символа.")]
    public string? BatchNumber { get; set; }

    public string ProfileType { get; set; } = "sheet";

    public decimal? ThicknessMm { get; set; }

    public decimal? WidthMm { get; set; }

    public decimal? LengthMm { get; set; }

    public decimal? DiameterMm { get; set; }

    public decimal? WallThicknessMm { get; set; }

    public IReadOnlyDictionary<Guid, string> MaterialProfileTypes { get; set; } = new Dictionary<Guid, string>();

    public IReadOnlyCollection<SelectListItem> Materials { get; set; } = Array.Empty<SelectListItem>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Quantity.HasValue || Quantity.Value <= 0)
        {
            yield break;
        }

        var profile = (ProfileType ?? string.Empty).Trim().ToLowerInvariant();
        if (profile != "sheet" && profile != "rod" && profile != "pipe")
        {
            yield return new ValidationResult("Не указан корректный тип проката.", new[] { nameof(ProfileType) });
            yield break;
        }

        if (!PassportWeightKg.HasValue || PassportWeightKg.Value <= 0m)
        {
            yield return new ValidationResult("Паспортная масса обязательна.", new[] { nameof(PassportWeightKg) });
        }

        if (!ActualWeightKg.HasValue || ActualWeightKg.Value <= 0m)
        {
            yield return new ValidationResult("Фактическая масса обязательна.", new[] { nameof(ActualWeightKg) });
        }

        if (profile == "sheet")
        {
            if (!ThicknessMm.HasValue || ThicknessMm.Value <= 0m || !WidthMm.HasValue || WidthMm.Value <= 0m || !LengthMm.HasValue || LengthMm.Value <= 0m)
            {
                yield return new ValidationResult("Для листа обязательны толщина, ширина и длина.", new[] { nameof(ThicknessMm), nameof(WidthMm), nameof(LengthMm) });
            }
        }
        else if (profile == "rod")
        {
            if (!DiameterMm.HasValue || DiameterMm.Value <= 0m || !LengthMm.HasValue || LengthMm.Value <= 0m)
            {
                yield return new ValidationResult("Для круга/прутка обязательны диаметр и длина хлыста.", new[] { nameof(DiameterMm), nameof(LengthMm) });
            }
        }
        else if (profile == "pipe")
        {
            if (!DiameterMm.HasValue || DiameterMm.Value <= 0m || !WallThicknessMm.HasValue || WallThicknessMm.Value <= 0m || !LengthMm.HasValue || LengthMm.Value <= 0m)
            {
                yield return new ValidationResult("Для трубы обязательны диаметр, стенка и длина.", new[] { nameof(DiameterMm), nameof(WallThicknessMm), nameof(LengthMm) });
            }
        }

        if (Units.Count != Quantity.Value)
        {
            yield return new ValidationResult(
                "Количество поштучных полей должно совпадать с количеством.",
                new[] { nameof(Units), nameof(Quantity) });
            yield break;
        }

        foreach (var unit in Units)
        {
            if (!unit.SizeValue.HasValue || unit.SizeValue.Value <= 0m)
            {
                yield return new ValidationResult(
                    $"Размер для единицы №{unit.ItemIndex} обязателен и должен быть больше 0.",
                    new[] { nameof(Units) });
            }
        }
    }
}

public class MetalReceiptDetailsItemViewModel
{
    public int ItemIndex { get; init; }

    public decimal SizeValue { get; init; }

    public string SizeUnitText { get; init; } = string.Empty;

    public string GeneratedCode { get; init; } = string.Empty;
}

public class MetalReceiptDetailsViewModel
{
    public Guid Id { get; init; }

    public string ReceiptNumber { get; init; } = string.Empty;

    public DateTime ReceiptDate { get; init; }

    public string SupplierOrSource { get; init; } = string.Empty;

    public string? Comment { get; init; }

    public string MaterialName { get; init; } = string.Empty;

    public decimal PassportWeightKg { get; init; }

    public decimal ActualWeightKg { get; init; }

    public decimal CalculatedWeightKg { get; init; }

    public decimal WeightDeviationKg { get; init; }

    public string BatchNumber { get; init; } = string.Empty;

    public string ProfileTypeDisplay { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public IReadOnlyCollection<MetalReceiptDetailsItemViewModel> Items { get; init; } = Array.Empty<MetalReceiptDetailsItemViewModel>();
}

public class MetalRequirementListItemViewModel
{
    public Guid Id { get; init; }

    public string RequirementNumber { get; init; } = string.Empty;

    public DateTime RequirementDate { get; init; }

    public string PartDisplay { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public string MaterialDisplay { get; init; } = string.Empty;

    public decimal RequiredQty { get; init; }

    public string Unit { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;
}

public class MetalRequirementListViewModel
{
    public IReadOnlyCollection<MetalRequirementListItemViewModel> Items { get; init; } = Array.Empty<MetalRequirementListItemViewModel>();
}

public class MetalRequirementDetailsItemViewModel
{
    public string MaterialDisplay { get; init; } = string.Empty;

    public string MaterialArticle { get; init; } = string.Empty;

    public decimal NormPerUnit { get; init; }

    public decimal TotalRequiredQty { get; init; }

    public string Unit { get; init; } = string.Empty;

    public decimal? TotalRequiredWeightKg { get; init; }

    public decimal NetRequirementQty { get; init; }

    public decimal LossFactor { get; init; }

    public decimal QtyToIssueFromStock { get; init; }

    public decimal ExpectedBusinessResidual { get; init; }

    public decimal ExpectedScrapResidual { get; init; }

    public decimal ExpectedStockAfterIssue => StockQty - QtyToIssueFromStock;

    public string SourceBlankDisplay { get; init; } = string.Empty;

    public Guid? CuttingPlanId { get; init; }

    public string? CalculationFormula { get; init; }

    public string? CalculationInput { get; init; }

    public decimal StockQty { get; init; }

    public decimal StockWeightKg { get; init; }

    public string SelectionSource { get; init; } = string.Empty;

    public string? SelectionReason { get; init; }

    public string? CandidateMaterials { get; init; }

    public decimal DifferenceQty => StockQty - QtyToIssueFromStock;

    public bool IsEnough => DifferenceQty >= 0m;

    public decimal BackCalculatedMeters { get; init; }

    public decimal BackCalculatedSquareMeters { get; init; }
}

public class MetalRequirementAggregateViewModel
{
    public decimal TotalKg { get; init; }

    public decimal TotalMeters { get; init; }

    public decimal TotalSquareMeters { get; init; }

    public decimal ForecastWastePercent { get; init; }

    public decimal ForecastBusinessResidual { get; init; }

    public decimal ForecastScrapResidual { get; init; }
}

public class MetalRequirementCutDetailViewModel
{
    public int StockIndex { get; init; }

    public int Sequence { get; init; }

    public string ItemType { get; init; } = string.Empty;

    public decimal? Length { get; init; }

    public decimal? Width { get; init; }

    public decimal? Height { get; init; }

    public decimal? PositionX { get; init; }

    public decimal? PositionY { get; init; }

    public bool Rotated { get; init; }

    public int Quantity { get; init; }
}

public class MetalRequirementDetailsViewModel
{
    public Guid Id { get; init; }

    public string RequirementNumber { get; init; } = string.Empty;

    public DateTime RequirementDate { get; init; }

    public string Status { get; init; } = string.Empty;

    public string PartDisplay { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public Guid WipLaunchId { get; init; }

    public DateTime? LaunchDate { get; init; }

    public string? Comment { get; init; }

    public string SourceBlankDisplay { get; init; } = string.Empty;

    public Guid? CurrentCuttingPlanId { get; init; }

    public MetalRequirementPlanViewModel? RequirementPlan { get; init; }

    public MetalRequirementAggregateViewModel Aggregates { get; init; } = new();

    public IReadOnlyCollection<MetalRequirementCutDetailViewModel> CutDetails { get; init; } = Array.Empty<MetalRequirementCutDetailViewModel>();

    public IReadOnlyCollection<MetalRequirementDetailsItemViewModel> Items { get; init; } = Array.Empty<MetalRequirementDetailsItemViewModel>();
}

public class MetalRequirementPlanViewModel
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal RequiredQty { get; init; }
    public decimal PlannedQty { get; init; }
    public decimal DeficitQty { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string? CalculationComment { get; init; }
    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime? RecalculatedAt { get; init; }
    public string? RecalculatedBy { get; init; }
    public IReadOnlyCollection<MetalRequirementPlanItemViewModel> Items { get; init; } = Array.Empty<MetalRequirementPlanItemViewModel>();
}

public class MetalRequirementPlanItemViewModel
{
    public string SourceCode { get; init; } = string.Empty;
    public decimal SourceSize { get; init; }
    public string SourceUnit { get; init; } = string.Empty;
    public decimal PlannedUseQty { get; init; }
    public decimal RemainingAfterQty { get; init; }
    public string LineStatus { get; init; } = string.Empty;
    public DateTime? ReceiptDate { get; init; }
}

public class CuttingMapListViewModel
{
    public IReadOnlyCollection<CuttingMapCardViewModel> Maps { get; init; } = Array.Empty<CuttingMapCardViewModel>();
}

public class CuttingMapCardViewModel
{
    public Guid PlanId { get; init; }
    public Guid RequirementId { get; init; }
    public string RequirementNumber { get; init; } = string.Empty;
    public string PartDisplay { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public int Version { get; init; }
    public DateTime CreatedAt { get; init; }
    public decimal UtilizationPercent { get; init; }
    public decimal WastePercent { get; init; }
    public int CutCount { get; init; }
    public decimal BusinessResidual { get; init; }
    public decimal ScrapResidual { get; init; }
    public string ExecutionStatus { get; init; } = "Не выполнено";
    public decimal? ActualResidual { get; init; }
    public string StockCaption { get; init; } = string.Empty;
    public IReadOnlyCollection<CuttingMapStockViewModel> Stocks { get; init; } = Array.Empty<CuttingMapStockViewModel>();
}

public class CuttingMapStockViewModel
{
    public int StockIndex { get; init; }
    public string StepDescription { get; init; } = string.Empty;
    public IReadOnlyCollection<CuttingMapPlacementViewModel> Placements { get; init; } = Array.Empty<CuttingMapPlacementViewModel>();
}

public class CuttingMapPlacementViewModel
{
    public string ItemType { get; init; } = string.Empty;
    public decimal? Length { get; init; }
    public decimal? Width { get; init; }
    public decimal? Height { get; init; }
    public decimal? PositionX { get; init; }
    public decimal? PositionY { get; init; }
    public bool Rotated { get; init; }
}

public class CuttingReportCreateViewModel
{
    [Required]
    public Guid CuttingPlanId { get; set; }

    [Required]
    public Guid SourceMetalReceiptItemId { get; set; }

    [Required]
    [StringLength(128)]
    public string Workshop { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    public string Shift { get; set; } = string.Empty;

    [Range(0, 999999999)]
    public decimal ActualProducedSize { get; set; }

    [Range(0, 999999999)]
    public decimal ActualProducedMassKg { get; set; }

    [Range(0, 999999999)]
    public decimal BusinessResidualSize { get; set; }

    [Range(0, 999999999)]
    public decimal BusinessResidualMassKg { get; set; }

    [Range(0, 999999999)]
    public decimal ScrapSize { get; set; }

    [Range(0, 999999999)]
    public decimal ScrapMassKg { get; set; }
}

public class CuttingReportListItemViewModel
{
    public Guid Id { get; init; }
    public string ReportNumber { get; init; } = string.Empty;
    public DateTime ReportDate { get; init; }
    public string RequirementNumber { get; init; } = string.Empty;
    public string MaterialName { get; init; } = string.Empty;
    public string SourceCode { get; init; } = string.Empty;
    public string Workshop { get; init; } = string.Empty;
    public string Shift { get; init; } = string.Empty;
    public decimal PlannedSize { get; init; }
    public decimal ActualProducedSize { get; init; }
    public decimal SizeDeviation => ActualProducedSize - PlannedSize;
    public decimal PlannedMassKg { get; init; }
    public decimal ActualProducedMassKg { get; init; }
    public decimal MassDeviation => ActualProducedMassKg - PlannedMassKg;
    public decimal PlannedWaste { get; init; }
    public decimal ActualWaste { get; init; }
    public decimal WasteDeviation => ActualWaste - PlannedWaste;
}

public class CuttingAnalyticsItemViewModel
{
    public string Workshop { get; init; } = string.Empty;
    public string Shift { get; init; } = string.Empty;
    public string MaterialName { get; init; } = string.Empty;
    public int ReportsCount { get; init; }
    public decimal AvgWasteDeviation { get; init; }
    public decimal TotalScrapMassKg { get; init; }
}

public class CuttingReportPageViewModel
{
    public CuttingReportCreateViewModel CreateModel { get; init; } = new();
    public IReadOnlyCollection<SelectListItem> PlanOptions { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyCollection<SelectListItem> SourceOptions { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyCollection<CuttingReportListItemViewModel> Reports { get; init; } = Array.Empty<CuttingReportListItemViewModel>();
    public IReadOnlyCollection<CuttingAnalyticsItemViewModel> Analytics { get; init; } = Array.Empty<CuttingAnalyticsItemViewModel>();
}
