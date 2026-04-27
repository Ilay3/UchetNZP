using System.ComponentModel.DataAnnotations;

namespace UchetNZP.Web.Models;

public class AdminMetalMaterialsPageViewModel
{
    public AdminMetalMaterialCreateInputModel CreateModel { get; init; } = new();
    public AdminMetalMaterialUpdateInputModel UpdateModel { get; init; } = new();

    public IReadOnlyCollection<AdminMetalMaterialListItemViewModel> Materials { get; init; } = Array.Empty<AdminMetalMaterialListItemViewModel>();
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
