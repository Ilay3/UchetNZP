using System.Globalization;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
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

    public bool ShowConsumed { get; init; }

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

    public string? DocumentNumber { get; init; }

    public string? SourceDocumentType { get; init; }

    public Guid? SourceDocumentId { get; init; }

    public string? UserName { get; init; }
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

    public string? ReceiptComment { get; init; }

    public string Status { get; init; } = "В наличии";

    public IReadOnlyCollection<MetalStockItemHistoryEntryViewModel> History { get; init; } = Array.Empty<MetalStockItemHistoryEntryViewModel>();
}

public class MetalReceiptListItemViewModel
{
    public Guid Id { get; init; }

    public string ReceiptNumber { get; init; } = string.Empty;

    public string? SupplierDisplay { get; init; }

    public string? SupplierDocumentNumber { get; init; }

    public string MaterialsSummary { get; init; } = string.Empty;

    public int TotalQuantity { get; init; }

    public decimal TotalPassportWeightKg { get; init; }

    public decimal PricePerKg { get; init; }

    public decimal AmountWithoutVat { get; init; }

    public decimal VatAmount { get; init; }

    public decimal TotalAmountWithVat { get; init; }

    public string SizeSummary { get; init; } = string.Empty;

    public bool HasOriginalDocument { get; init; }

    public string MaterialName => MaterialsSummary;

    public int Quantity => TotalQuantity;

    public decimal PassportWeightKg => TotalPassportWeightKg;
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

public class MetalReceiptLineInputViewModel
{
    [Range(0.0001d, 999999999999d, ErrorMessage = "Цена должна быть больше 0.")]
    public decimal? PricePerKg { get; set; }

    public Guid? MetalMaterialId { get; set; }

    public string? MaterialInputText { get; set; }

    [Range(0.000001d, 999999999999d, ErrorMessage = "Вес должен быть больше 0.")]
    public decimal? PassportWeightKg { get; set; }

    [Range(1, 9999, ErrorMessage = "Количество должно быть больше 0.")]
    public int? Quantity { get; set; }

    public bool UseAverageSize { get; set; }

    [Range(0.000001d, 999999999999d, ErrorMessage = "Средний размер должен быть больше 0.")]
    public decimal? AverageSizeValue { get; set; }

    public List<MetalReceiptUnitInputViewModel> Units { get; set; } = new();
}

public class MetalReceiptCreateViewModel : IValidatableObject
{
    public const long MaxOriginalDocumentSizeBytes = 25L * 1024L * 1024L;

    [Required(ErrorMessage = "Дата прихода обязательна.")]
    [DataType(DataType.Date)]
    public DateTime? ReceiptDate { get; set; }

    public Guid? SupplierId { get; set; }

    public string? SupplierInputText { get; set; }

    [Required(ErrorMessage = "Номер документа поставщика обязателен.")]
    [StringLength(128, ErrorMessage = "Номер документа не должен превышать 128 символов.")]
    public string? SupplierDocumentNumber { get; set; }

    [StringLength(128, ErrorMessage = "Накладная/УПД не должна превышать 128 символов.")]
    public string? InvoiceOrUpiNumber { get; set; }

    public string AccountingAccount { get; set; } = "10.01";

    public string VatAccount { get; set; } = "19.03";

    public decimal? PricePerKg { get; set; }

    public decimal VatRatePercent { get; set; }

    public decimal AmountWithoutVat { get; set; }

    public decimal VatAmount { get; set; }

    public decimal TotalAmountWithVat { get; set; }

    public Guid? MetalMaterialId { get; set; }

    [Range(0.000001d, 999999999999d, ErrorMessage = "Вес должен быть больше 0.")]
    public decimal? PassportWeightKg { get; set; }

    public decimal? TotalWeightKg
    {
        get => PassportWeightKg;
        set => PassportWeightKg = value;
    }

    [Range(1, 9999, ErrorMessage = "Количество должно быть больше 0.")]
    public int? Quantity { get; set; }

    [StringLength(256, ErrorMessage = "Комментарий не должен превышать 256 символов.")]
    public string? Comment { get; set; }

    public List<MetalReceiptUnitInputViewModel> Units { get; set; } = new();

    public List<MetalReceiptLineInputViewModel> Items { get; set; } = new();

    public IFormFile? OriginalDocumentPdf { get; set; }

    public IReadOnlyDictionary<Guid, string> MaterialUnitKinds { get; set; } = new Dictionary<Guid, string>();

    public IReadOnlyDictionary<Guid, decimal> MaterialCoefficients { get; set; } = new Dictionary<Guid, decimal>();

    public IReadOnlyDictionary<Guid, decimal> MaterialWeightPerUnitKg { get; set; } = new Dictionary<Guid, decimal>();

    public IReadOnlyCollection<SelectListItem> Materials { get; set; } = Array.Empty<SelectListItem>();

    public IReadOnlyCollection<SelectListItem> Suppliers { get; set; } = Array.Empty<SelectListItem>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (OriginalDocumentPdf is { Length: > 0 } file)
        {
            if (file.Length > MaxOriginalDocumentSizeBytes)
            {
                yield return new ValidationResult(
                    "PDF слишком большой. Максимум 25 МБ.",
                    new[] { nameof(OriginalDocumentPdf) });
            }

            if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                yield return new ValidationResult(
                    "Можно прикрепить только PDF-файл.",
                    new[] { nameof(OriginalDocumentPdf) });
            }
        }

        var lines = Items.Count > 0
            ? Items
            : BuildLegacyLines();

        if (lines.Count == 0)
        {
            yield return new ValidationResult("Добавьте хотя бы один металл.", new[] { nameof(Items) });
            yield break;
        }

        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex];
            var lineNumber = lineIndex + 1;

            if (!line.MetalMaterialId.HasValue)
            {
                yield return new ValidationResult(
                    $"Выберите материал в строке {lineNumber}.",
                    new[] { $"{nameof(Items)}[{lineIndex}].{nameof(MetalReceiptLineInputViewModel.MetalMaterialId)}" });
            }

            if (!line.PassportWeightKg.HasValue || line.PassportWeightKg.Value <= 0m)
            {
                yield return new ValidationResult(
                    $"Укажите вес по документу в строке {lineNumber}.",
                    new[] { $"{nameof(Items)}[{lineIndex}].{nameof(MetalReceiptLineInputViewModel.PassportWeightKg)}" });
            }

            if (!line.PricePerKg.HasValue || line.PricePerKg.Value <= 0m)
            {
                yield return new ValidationResult(
                    $"Укажите цену в строке {lineNumber}.",
                    new[] { $"{nameof(Items)}[{lineIndex}].{nameof(MetalReceiptLineInputViewModel.PricePerKg)}" });
            }

            if (!line.Quantity.HasValue || line.Quantity.Value <= 0)
            {
                yield return new ValidationResult(
                    $"Укажите количество в строке {lineNumber}.",
                    new[] { $"{nameof(Items)}[{lineIndex}].{nameof(MetalReceiptLineInputViewModel.Quantity)}" });
                continue;
            }

            if (line.UseAverageSize)
            {
                if (!line.AverageSizeValue.HasValue || line.AverageSizeValue.Value <= 0m)
                {
                    yield return new ValidationResult(
                        $"Укажите средний размер в строке {lineNumber}.",
                        new[] { $"{nameof(Items)}[{lineIndex}].{nameof(MetalReceiptLineInputViewModel.AverageSizeValue)}" });
                }

                continue;
            }

            if (line.Units.Count != line.Quantity.Value)
            {
                yield return new ValidationResult(
                    $"Количество полей размеров в строке {lineNumber} должно совпадать с количеством.",
                    new[] { $"{nameof(Items)}[{lineIndex}].{nameof(MetalReceiptLineInputViewModel.Units)}" });
                continue;
            }

            foreach (var unit in line.Units.Where(x => x.SizeValue.HasValue))
            {
                if (unit.SizeValue!.Value <= 0m)
                {
                    yield return new ValidationResult(
                        $"Размер для единицы №{unit.ItemIndex} в строке {lineNumber} должен быть больше 0.",
                        new[] { $"{nameof(Items)}[{lineIndex}].{nameof(MetalReceiptLineInputViewModel.Units)}" });
                }
            }
        }
    }

    private List<MetalReceiptLineInputViewModel> BuildLegacyLines()
    {
        if (!MetalMaterialId.HasValue && !Quantity.HasValue && !PassportWeightKg.HasValue && Units.Count == 0)
        {
            return new List<MetalReceiptLineInputViewModel>();
        }

        return new List<MetalReceiptLineInputViewModel>
        {
            new()
            {
                MetalMaterialId = MetalMaterialId,
                PassportWeightKg = PassportWeightKg,
                PricePerKg = PricePerKg,
                Quantity = Quantity,
                Units = Units,
            },
        };
    }

    private IEnumerable<ValidationResult> ValidateLegacy(ValidationContext validationContext)
    {
        if (!Quantity.HasValue || Quantity.Value <= 0)
        {
            yield break;
        }

        if (!PassportWeightKg.HasValue || PassportWeightKg.Value <= 0m)
        {
            yield return new ValidationResult("Паспортная масса обязательна.", new[] { nameof(PassportWeightKg) });
        }

        if (Units.Count != Quantity.Value)
        {
            yield return new ValidationResult(
                "Количество поштучных полей должно совпадать с количеством.",
                new[] { nameof(Units), nameof(Quantity) });
            yield break;
        }

        // Размер по единицам может быть не заполнен вручную:
        // значение будет рассчитано автоматически из массы/геометрии в контроллере.
        foreach (var unit in Units.Where(x => x.SizeValue.HasValue))
        {
            if (unit.SizeValue!.Value <= 0m)
            {
                yield return new ValidationResult(
                    $"Размер для единицы №{unit.ItemIndex} должен быть больше 0.",
                    new[] { nameof(Units) });
            }
        }
    }
}

public class MetalSupplierListItemViewModel
{
    public Guid Id { get; init; }
    public string Identifier { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Inn { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public class MetalSuppliersDirectoryViewModel
{
    [Required(ErrorMessage = "Код поставщика обязателен.")]
    [StringLength(32)]
    public string Identifier { get; set; } = string.Empty;

    [Required(ErrorMessage = "Наименование обязательно.")]
    [StringLength(256)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "ИНН обязателен.")]
    [StringLength(12)]
    public string Inn { get; set; } = string.Empty;

    public IReadOnlyCollection<MetalSupplierListItemViewModel> Suppliers { get; set; } = Array.Empty<MetalSupplierListItemViewModel>();
}

public class MetalSupplierInlineCreateModel
{
    public string Identifier { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Inn { get; set; } = string.Empty;
}

public class MetalMaterialInlineCreateModel
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string UnitKind { get; set; } = "Meter";
    public decimal WeightPerUnitKg { get; set; } = 1m;
}

public class MetalReceiptDetailsItemViewModel
{
    public Guid Id { get; init; }

    public int ReceiptLineIndex { get; init; }

    public int ItemIndex { get; init; }

    public string MaterialName { get; init; } = string.Empty;

    public decimal SizeValue { get; init; }

    public string SizeUnitText { get; init; } = string.Empty;
    
    public string ActualBlankSizeText { get; init; } = string.Empty;

    public bool IsSizeApproximate { get; init; }

    public string GeneratedCode { get; init; } = string.Empty;
}

public class MetalReceiptDetailsLineViewModel
{
    public int LineIndex { get; init; }

    public string MaterialName { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal PassportWeightKg { get; init; }

    public decimal CalculatedWeightKg { get; init; }

    public decimal WeightDeviationKg { get; init; }

    public string CalculatedWeightFormula { get; init; } = string.Empty;

    public string WeightDeviationFormula { get; init; } = string.Empty;

    public string SizeSummary { get; init; } = string.Empty;

    public bool UsesAverageSize { get; init; }
}

public class MetalReceiptDetailsViewModel
{
    public Guid Id { get; init; }

    public string ReceiptNumber { get; init; } = string.Empty;

    public DateTime ReceiptDate { get; init; }

    public string SupplierDisplay { get; init; } = string.Empty;

    public string? SupplierDocumentNumber { get; init; }

    public string? Comment { get; init; }

    public string MaterialName { get; init; } = string.Empty;

    public decimal PassportWeightKg { get; init; }

    public decimal CalculatedWeightKg { get; init; }

    public decimal WeightDeviationKg { get; init; }

    public string CalculatedWeightFormula { get; init; } = string.Empty;

    public string WeightDeviationFormula { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal PricePerKg { get; init; }

    public decimal AmountWithoutVat { get; init; }

    public decimal VatRatePercent { get; init; }

    public decimal VatAmount { get; init; }

    public decimal TotalAmountWithVat { get; init; }

    public bool HasOriginalDocument { get; init; }

    public string? OriginalDocumentFileName { get; init; }

    public IReadOnlyCollection<MetalReceiptDetailsLineViewModel> Lines { get; init; } = Array.Empty<MetalReceiptDetailsLineViewModel>();

    public IReadOnlyCollection<MetalReceiptDetailsItemViewModel> Items { get; init; } = Array.Empty<MetalReceiptDetailsItemViewModel>();
}

public class MetalRequirementListItemViewModel
{
    public Guid Id { get; init; }

    public string RequirementNumber { get; init; } = string.Empty;

    public DateTime RequirementDate { get; init; }

    public string PartDisplay { get; init; } = string.Empty;

    public string MaterialDisplay { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

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

    public string MaterialDisplay { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public Guid WipLaunchId { get; init; }

    public DateTime? LaunchDate { get; init; }

    public string? Comment { get; init; }

    public string SourceBlankDisplay { get; init; } = string.Empty;

    public Guid? CurrentCuttingPlanId { get; init; }

    public MetalRequirementPlanViewModel? RequirementPlan { get; init; }

    public Guid? ExistingIssueId { get; init; }

    public string? ExistingIssueStatus { get; init; }

    public bool HasSelectionPlan { get; init; }

    public decimal PlanDeficitQty { get; init; }

    public string PlanUnit { get; init; } = string.Empty;

    public bool CanCreateIssueFromPlan { get; set; }

    public string IssueCreationBlockedReason { get; set; } = string.Empty;

    public MetalRequirementAggregateViewModel Aggregates { get; init; } = new();

    public IReadOnlyCollection<MetalRequirementCutDetailViewModel> CutDetails { get; init; } = Array.Empty<MetalRequirementCutDetailViewModel>();

    public IReadOnlyCollection<MetalRequirementDetailsItemViewModel> Items { get; init; } = Array.Empty<MetalRequirementDetailsItemViewModel>();

    public IReadOnlyCollection<MetalAuditLogEntryViewModel> History { get; init; } = Array.Empty<MetalAuditLogEntryViewModel>();
}

public class MetalRequirementPlanViewModel
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal BaseRequiredQty { get; init; }
    public decimal AdjustedRequiredQty { get; init; }
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
    public Guid? MetalReceiptItemId { get; init; }
    public string SourceCode { get; init; } = string.Empty;
    public decimal SourceSize { get; init; }
    public string SourceUnit { get; init; } = string.Empty;
    public decimal PlannedUseQty { get; init; }
    public decimal RemainingAfterQty { get; init; }
    public string LineStatus { get; init; } = string.Empty;
    public DateTime? ReceiptDate { get; init; }
}

public class MetalIssueListViewModel
{
    public IReadOnlyCollection<MetalIssueListItemViewModel> Items { get; init; } = Array.Empty<MetalIssueListItemViewModel>();
}

public class MetalIssueListItemViewModel
{
    public Guid Id { get; init; }
    public string IssueNumber { get; init; } = string.Empty;
    public DateTime IssueDate { get; init; }
    public string RequirementNumber { get; init; } = string.Empty;
    public string PartDisplay { get; init; } = string.Empty;
    public string MaterialDisplay { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

public class MetalIssueDetailsViewModel
{
    public Guid Id { get; init; }
    public string IssueNumber { get; init; } = string.Empty;
    public DateTime IssueDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public string RequirementNumber { get; init; } = string.Empty;
    public Guid RequirementId { get; init; }
    public string PartDisplay { get; init; } = string.Empty;
    public string MaterialDisplay { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string? Comment { get; init; }
    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime? CompletedAt { get; init; }
    public string? CompletedBy { get; init; }
    public bool CanComplete { get; init; }
    public IReadOnlyCollection<MetalIssueDetailsItemViewModel> Items { get; init; } = Array.Empty<MetalIssueDetailsItemViewModel>();
    public IReadOnlyCollection<MetalAuditLogEntryViewModel> History { get; init; } = Array.Empty<MetalAuditLogEntryViewModel>();
}

public class MetalAuditLogEntryViewModel
{
    public DateTime EventDate { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string UserName { get; init; } = "system";
    public string Message { get; init; } = string.Empty;
}

public class MetalMovementsFilterViewModel
{
    [DataType(DataType.Date)]
    public DateTime? DateFrom { get; init; }
    [DataType(DataType.Date)]
    public DateTime? DateTo { get; init; }
    public Guid? MaterialId { get; init; }
    public string? MovementType { get; init; }
    public string? DocumentNumber { get; init; }
    public string? MetalUnitCode { get; init; }
    public IReadOnlyCollection<SelectListItem> Materials { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyCollection<SelectListItem> MovementTypes { get; init; } = Array.Empty<SelectListItem>();
}

public class MetalMovementRowViewModel
{
    public DateTime Date { get; init; }
    public string MovementType { get; init; } = string.Empty;
    public string Material { get; init; } = string.Empty;
    public string MetalUnitCode { get; init; } = string.Empty;
    public string Document { get; init; } = string.Empty;
    public decimal? Before { get; init; }
    public decimal Change { get; init; }
    public decimal? After { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string User { get; init; } = string.Empty;
}

public class MetalMovementsPageViewModel
{
    public MetalMovementsFilterViewModel Filter { get; init; } = new();
    public IReadOnlyCollection<MetalMovementRowViewModel> Rows { get; init; } = Array.Empty<MetalMovementRowViewModel>();
}

public class MetalIssueDetailsItemViewModel
{
    public string SourceCode { get; init; } = string.Empty;
    public decimal SourceQtyBefore { get; init; }
    public decimal IssuedQty { get; init; }
    public decimal RemainingQtyAfter { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string LineStatus { get; init; } = string.Empty;
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
