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

    public string? FullName { get; init; }

    public string? Code { get; init; }

    public string? Article { get; init; }

    public string UnitOfMeasure { get; init; } = "кг";

    public string? NomenclatureType { get; init; }

    public string? NomenclatureGroup { get; init; }

    public string? VatRateType { get; init; }

    public string? CountryOfOrigin { get; init; }

    public string? CustomsDeclarationNumber { get; init; }

    public string? TnVedCode { get; init; }

    public string? Okpd2Code { get; init; }

    public string? Comment { get; init; }

    public bool IsService { get; init; }

    public string UnitKind { get; init; } = "Meter";

    public string StockUnit { get; init; } = "m";

    public decimal MassPerMeterKg { get; init; }

    public decimal MassPerSquareMeterKg { get; init; }

    public decimal CoefConsumption { get; init; }

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

    [StringLength(512, ErrorMessage = "Полное наименование не должно превышать 512 символов.")]
    public string? FullName { get; set; }

    [StringLength(128, ErrorMessage = "Артикул не должен превышать 128 символов.")]
    public string? Article { get; set; }

    [Required(ErrorMessage = "Единица 1С обязательна.")]
    [StringLength(32, ErrorMessage = "Единица 1С не должна превышать 32 символа.")]
    public string UnitOfMeasure { get; set; } = "кг";

    [StringLength(128, ErrorMessage = "Вид номенклатуры не должен превышать 128 символов.")]
    public string? NomenclatureType { get; set; } = "Материалы";

    [StringLength(128, ErrorMessage = "Номенклатурная группа не должна превышать 128 символов.")]
    public string? NomenclatureGroup { get; set; } = "Металл";

    [StringLength(64, ErrorMessage = "Вид ставки НДС не должен превышать 64 символа.")]
    public string? VatRateType { get; set; } = "НДС 22%";

    [StringLength(128, ErrorMessage = "Страна происхождения не должна превышать 128 символов.")]
    public string? CountryOfOrigin { get; set; }

    [StringLength(64, ErrorMessage = "Номер ГТД не должен превышать 64 символа.")]
    public string? CustomsDeclarationNumber { get; set; }

    [StringLength(32, ErrorMessage = "ОКПД2 не должен превышать 32 символа.")]
    public string? Okpd2Code { get; set; }

    [StringLength(32, ErrorMessage = "ТН ВЭД не должен превышать 32 символа.")]
    public string? TnVedCode { get; set; }

    [StringLength(1024, ErrorMessage = "Комментарий не должен превышать 1024 символа.")]
    public string? Comment { get; set; }

    public bool IsService { get; set; }

    [Required(ErrorMessage = "Тип складской единицы обязателен.")]
    [StringLength(32, ErrorMessage = "Тип складской единицы не должен превышать 32 символа.")]
    public string UnitKind { get; set; } = "Meter";

    [StringLength(16, ErrorMessage = "Складская единица не должна превышать 16 символов.")]
    public string? StockUnit { get; set; } = "m";

    [Range(0d, 999999999999d, ErrorMessage = "Масса 1м должна быть не меньше 0.")]
    public decimal? MassPerMeterKg { get; set; }

    [Range(0d, 999999999999d, ErrorMessage = "Масса 1м² должна быть не меньше 0.")]
    public decimal? MassPerSquareMeterKg { get; set; }

    [Range(0.000001d, 999999999999d, ErrorMessage = "Коэффициент расхода должен быть больше 0.")]
    public decimal? CoefConsumption { get; set; } = 1m;

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
