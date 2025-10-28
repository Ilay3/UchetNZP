using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using UchetNZP.Shared;

namespace UchetNZP.Web.Models;

public record RouteListItemViewModel(
    Guid Id,
    Guid PartId,
    string PartName,
    string? PartCode,
    string OpNumber,
    string OperationName,
    Guid SectionId,
    string SectionName,
    decimal NormHours)
{
    public string PartDisplayName => NameWithCodeFormatter.getNameWithCode(PartName, PartCode);

    public string OperationDisplay => string.IsNullOrWhiteSpace(OperationName)
        ? OpNumber
        : $"{OpNumber} — {OperationName}";
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

public class RouteEditInputModel : IValidatableObject
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Заполните наименование детали.")]
    [StringLength(256, ErrorMessage = "Наименование детали не должно превышать 256 символов.")]
    [Display(Name = "Деталь")]
    public string PartName { get; set; } = string.Empty;

    [Display(Name = "Наименование операции")]
    public string? OperationName { get; set; }
        = string.Empty;

    public const string OpNumberPattern = OperationNumber.AllowedPattern;

    [Required(ErrorMessage = "Укажите номер операции.")]
    [RegularExpression(OpNumberPattern, ErrorMessage = "Номер операции должен состоять из 1–10 цифр и может содержать дробную часть через «/».")]
    [Display(Name = "№ операции")]
    public string OpNumber { get; set; } = string.Empty;

    [Display(Name = "Норматив (н/ч)")]
    public decimal NormHours { get; set; }

    [Required(ErrorMessage = "Укажите вид работ.")]
    [StringLength(256, ErrorMessage = "Наименование вида работ не должно превышать 256 символов.")]
    [Display(Name = "Вид работ")]
    public string SectionName { get; set; } = string.Empty;

    public string Title => Id.HasValue ? "Редактирование маршрута" : "Добавление маршрута";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (NormHours <= 0)
        {
            yield return new ValidationResult(
                "Норматив должен быть больше нуля.",
                new[] { nameof(NormHours) });
        }
    }

    public int GetOpNumberValue()
    {
        if (string.IsNullOrWhiteSpace(OpNumber))
        {
            throw new ValidationException("Номер операции не заполнен.");
        }

        return OperationNumber.Parse(OpNumber, nameof(OpNumber));
    }
}
