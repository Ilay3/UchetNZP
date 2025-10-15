using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace UchetNZP.Web.Models;

public record RouteListItemViewModel(
    Guid Id,
    Guid PartId,
    string PartName,
    string? PartCode,
    int OpNumber,
    string OperationName,
    Guid SectionId,
    string SectionName,
    decimal NormHours)
{
    public string PartDisplayName => string.IsNullOrWhiteSpace(PartCode)
        ? PartName
        : $"{PartName} ({PartCode})";

    public string OperationDisplay => string.IsNullOrWhiteSpace(OperationName)
        ? OpNumber.ToString("D3")
        : $"{OpNumber:D3} — {OperationName}";
}

public class RouteListFilterViewModel
{
    public string? Search { get; init; }

    public Guid? SectionId { get; init; }

    public IReadOnlyList<LookupItemViewModel> Sections { get; init; } = Array.Empty<LookupItemViewModel>();
}

public class RouteListViewModel
{
    public RouteListViewModel(RouteListFilterViewModel filter, IReadOnlyList<RouteListItemViewModel> routes)
    {
        Filter = filter ?? throw new ArgumentNullException(nameof(filter));
        Routes = routes ?? throw new ArgumentNullException(nameof(routes));
    }

    public RouteListFilterViewModel Filter { get; }

    public IReadOnlyList<RouteListItemViewModel> Routes { get; }

    public int Total => Routes.Count;
}

public class RouteEditInputModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Заполните наименование детали.")]
    [StringLength(256, ErrorMessage = "Наименование детали не должно превышать 256 символов.")]
    [Display(Name = "Деталь")]
    public string PartName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите наименование операции.")]
    [StringLength(256, ErrorMessage = "Наименование операции не должно превышать 256 символов.")]
    [Display(Name = "Операция")]
    public string OperationName { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Номер операции должен быть больше нуля.")]
    [Display(Name = "№ операции")]
    public int OpNumber { get; set; }

    [Range(typeof(decimal), "0.001", "79228162514264337593543950335", ErrorMessage = "Норматив должен быть больше нуля.")]
    [Display(Name = "Норматив (н/ч)")]
    public decimal NormHours { get; set; }

    [Required(ErrorMessage = "Укажите участок.")]
    [StringLength(256, ErrorMessage = "Наименование участка не должно превышать 256 символов.")]
    [Display(Name = "Участок")]
    public string SectionName { get; set; } = string.Empty;

    public string Title => Id.HasValue ? "Редактирование маршрута" : "Добавление маршрута";
}
