using System.ComponentModel.DataAnnotations;

namespace UchetNZP.Web.Models;

public class AdminMetalMaterialsPageViewModel
{
    public AdminMetalMaterialCreateInputModel CreateModel { get; init; } = new();

    public IReadOnlyCollection<AdminMetalMaterialListItemViewModel> Materials { get; init; } = Array.Empty<AdminMetalMaterialListItemViewModel>();
}

public class AdminMetalMaterialListItemViewModel
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Code { get; init; }

    public string ProfileType { get; init; } = string.Empty;

    public decimal MassPerUnitKg { get; init; }

    public string UnitKind { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}

public class AdminMetalMaterialCreateInputModel
{
    [Required(ErrorMessage = "Название обязательно.")]
    [StringLength(256, ErrorMessage = "Название не должно превышать 256 символов.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(64, ErrorMessage = "Код не должен превышать 64 символа.")]
    public string? Code { get; set; }

    [Range(0.000001d, 999999999999d, ErrorMessage = "Масса должна быть больше 0.")]
    public decimal? MassPerUnitKg { get; set; }

    [Required(ErrorMessage = "Укажите тип профиля.")]
    public string ProfileType { get; set; } = "sheet";

    public bool IsActive { get; set; } = true;
}
