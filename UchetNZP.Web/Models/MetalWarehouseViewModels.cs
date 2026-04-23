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

    [Required(ErrorMessage = "Размер обязателен.")]
    [Range(0.000001d, 999999999999d, ErrorMessage = "Размер должен быть больше 0.")]
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
    public decimal? TotalWeightKg { get; set; }

    [Range(1, 9999, ErrorMessage = "Количество должно быть больше 0.")]
    public int? Quantity { get; set; }

    [StringLength(256, ErrorMessage = "Комментарий не должен превышать 256 символов.")]
    public string? Comment { get; set; }

    public List<MetalReceiptUnitInputViewModel> Units { get; set; } = new();

    public IReadOnlyCollection<SelectListItem> Materials { get; set; } = Array.Empty<SelectListItem>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Quantity.HasValue || Quantity.Value <= 0)
        {
            yield break;
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

    public decimal TotalWeightKg { get; init; }

    public int Quantity { get; init; }

    public IReadOnlyCollection<MetalReceiptDetailsItemViewModel> Items { get; init; } = Array.Empty<MetalReceiptDetailsItemViewModel>();
}
