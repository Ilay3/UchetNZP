using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace UchetNZP.Web.Models;

public class AdminIndexViewModel
{
    public IReadOnlyCollection<AdminEntityRowViewModel> Parts { get; init; } = Array.Empty<AdminEntityRowViewModel>();

    public IReadOnlyCollection<AdminEntityRowViewModel> Operations { get; init; } = Array.Empty<AdminEntityRowViewModel>();

    public IReadOnlyCollection<AdminEntityRowViewModel> Sections { get; init; } = Array.Empty<AdminEntityRowViewModel>();

    public IReadOnlyCollection<AdminCatalogWipBalanceRowViewModel> WipBalances { get; init; } = Array.Empty<AdminCatalogWipBalanceRowViewModel>();

    public AdminPartInputModel PartInput { get; init; } = new();

    public AdminOperationInputModel OperationInput { get; init; } = new();

    public AdminSectionInputModel SectionInput { get; init; } = new();

    public AdminWipBalanceInputModel WipBalanceInput { get; init; } = new();

    public IReadOnlyCollection<SelectListItem> PartOptions { get; init; } = Array.Empty<SelectListItem>();

    public IReadOnlyCollection<SelectListItem> SectionOptions { get; init; } = Array.Empty<SelectListItem>();

    public string? PartSearch { get; init; }

    public string? OperationSearch { get; init; }

    public string? SectionSearch { get; init; }

    public Guid? WipBalancePartFilter { get; init; }

    public Guid? WipBalanceSectionFilter { get; init; }

    public string? WipBalanceSearch { get; init; }

    public string? StatusMessage { get; init; }

    public string? ErrorMessage { get; init; }
}

public class AdminEntityRowViewModel
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Code { get; init; }
}

public class AdminCatalogWipBalanceRowViewModel
{
    public Guid Id { get; init; }

    public Guid PartId { get; init; }

    public string PartName { get; init; } = string.Empty;

    public Guid SectionId { get; init; }

    public string SectionName { get; init; } = string.Empty;

    public int OpNumber { get; init; }

    public decimal Quantity { get; init; }
}

public class AdminPartInputModel
{
    [Required]
    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? Code { get; set; }
}

public class AdminPartUpdateInputModel : AdminPartInputModel
{
    [Required]
    public Guid Id { get; set; }
}

public class AdminOperationInputModel
{
    [Required]
    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? Code { get; set; }
}

public class AdminOperationUpdateInputModel : AdminOperationInputModel
{
    [Required]
    public Guid Id { get; set; }
}

public class AdminSectionInputModel
{
    [Required]
    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? Code { get; set; }
}

public class AdminSectionUpdateInputModel : AdminSectionInputModel
{
    [Required]
    public Guid Id { get; set; }
}

public class AdminWipBalanceInputModel
{
    [Required]
    public Guid PartId { get; set; }

    [Required]
    public Guid SectionId { get; set; }

    [Range(0, int.MaxValue)]
    public int OpNumber { get; set; }

    [Range(0, 999999999)]
    public decimal Quantity { get; set; }
}

public class AdminWipBalanceUpdateInputModel : AdminWipBalanceInputModel
{
    [Required]
    public Guid Id { get; set; }
}

public class AdminFilterInputViewModel
{
    public AdminFilterInputViewModel(
        string id,
        string name,
        string label,
        string? value = null,
        string type = "text",
        string? placeholder = null,
        string cssClass = "col-md-4",
        IEnumerable<SelectListItem>? options = null)
    {
        Id = id;
        Name = name;
        Label = label;
        Value = value;
        Type = type;
        Placeholder = placeholder;
        CssClass = cssClass;
        Options = options;
    }

    public string Id { get; }

    public string Name { get; }

    public string Label { get; }

    public string Type { get; }

    public string? Placeholder { get; }

    public string? Value { get; }

    public string CssClass { get; }

    public IEnumerable<SelectListItem>? Options { get; }
}

public class AdminFilterPanelViewModel
{
    public string FormId { get; init; } = string.Empty;

    public string TargetTableId { get; init; } = string.Empty;

    public string SubmitLabel { get; init; } = "Применить";

    public IReadOnlyCollection<AdminFilterInputViewModel> Inputs { get; init; } = Array.Empty<AdminFilterInputViewModel>();
}

public class AdminTableActionsViewModel
{
    public string TemplateId { get; init; } = string.Empty;

    public string EntityKey { get; init; } = string.Empty;
}
