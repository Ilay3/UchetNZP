using System.ComponentModel.DataAnnotations;

namespace UchetNZP.Web.Models;

public class AdminMetalMaterialsPageViewModel
{
    public AdminMetalMaterialCreateInputModel CreateModel { get; init; } = new();
    public AdminMetalMaterialUpdateInputModel UpdateModel { get; init; } = new();

    public IReadOnlyCollection<AdminMetalMaterialListItemViewModel> Materials { get; init; } = Array.Empty<AdminMetalMaterialListItemViewModel>();
    public IReadOnlyCollection<AdminPartMaterialNormListItemViewModel> PartMaterialNorms { get; init; } = Array.Empty<AdminPartMaterialNormListItemViewModel>();
    public IReadOnlyCollection<LookupItemViewModel> Parts { get; init; } = Array.Empty<LookupItemViewModel>();
    public IReadOnlyCollection<LookupItemViewModel> ActiveMaterials { get; init; } = Array.Empty<LookupItemViewModel>();
    public AdminPartMaterialNormCreateInputModel NormCreateModel { get; init; } = new();
}

public class AdminMetalMaterialListItemViewModel
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Code { get; init; }

    public decimal WeightPerUnitKg { get; init; }

    public decimal Coefficient { get; init; }

    public bool IsActive { get; init; }
}

public class AdminMetalMaterialCreateInputModel
{
    [Required(ErrorMessage = "Название обязательно.")]
    [StringLength(256, ErrorMessage = "Название не должно превышать 256 символов.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(64, ErrorMessage = "Код не должен превышать 64 символа.")]
    public string? Code { get; set; }

    [Range(0.000001d, 999999999999d, ErrorMessage = "Вес должен быть больше 0.")]
    public decimal? WeightPerUnitKg { get; set; }

    [Range(0.000001d, 999999999999d, ErrorMessage = "Коэффициент должен быть больше 0.")]
    public decimal? Coefficient { get; set; } = 1m;

    public bool IsActive { get; set; } = true;
}

public class AdminMetalMaterialUpdateInputModel : AdminMetalMaterialCreateInputModel
{
    [Required]
    public Guid Id { get; set; }
}

public class AdminPartMaterialNormCreateInputModel
{
    [Required]
    public Guid PartId { get; set; }
    [Required]
    public Guid MetalMaterialId { get; set; }
}

public class AdminPartMaterialNormListItemViewModel
{
    public Guid Id { get; init; }
    public Guid PartId { get; init; }
    public string PartName { get; init; } = string.Empty;
    public string? PartCode { get; init; }
    public Guid MaterialId { get; init; }
    public string MaterialName { get; init; } = string.Empty;
    public string? MaterialCode { get; init; }
    public decimal BaseConsumptionQty { get; init; }
}
