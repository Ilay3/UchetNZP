using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace UchetNZP.Web.Models;

public class AdminWipIndexViewModel
{
    public Guid? SelectedPartId { get; init; }

    public Guid? SelectedSectionId { get; init; }

    public string? SelectedOpNumber { get; init; }

    public IReadOnlyCollection<SelectListItem> Parts { get; init; } = Array.Empty<SelectListItem>();

    public IReadOnlyCollection<SelectListItem> Sections { get; init; } = Array.Empty<SelectListItem>();

    public IReadOnlyCollection<AdminWipBalanceRowViewModel> Balances { get; init; } = Array.Empty<AdminWipBalanceRowViewModel>();

    public IReadOnlyCollection<AdminWipLabelRowViewModel> Labels { get; init; } = Array.Empty<AdminWipLabelRowViewModel>();

    public string? StatusMessage { get; init; }

    public string? ErrorMessage { get; init; }
}

public class AdminWipBalanceRowViewModel
{
    public Guid BalanceId { get; init; }

    public Guid PartId { get; init; }

    public Guid SectionId { get; init; }

    public string PartDisplay { get; init; } = string.Empty;

    public string SectionDisplay { get; init; } = string.Empty;

    public string OpNumber { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public IReadOnlyList<string> LabelNumbers { get; init; } = Array.Empty<string>();
}

public class AdminWipLabelRowViewModel
{
    public Guid LabelId { get; init; }

    public string Number { get; init; } = string.Empty;

    public string PartDisplay { get; init; } = string.Empty;

    public string? CurrentSectionDisplay { get; init; }

    public string? CurrentOpNumber { get; init; }

    public string Status { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public decimal RemainingQuantity { get; init; }
}

public class AdminWipAdjustmentInputModel
{
    [Required]
    public Guid BalanceId { get; set; }

    [Range(0, 999_999_999)]
    public decimal NewQuantity { get; set; }

    [MaxLength(512)]
    public string? Comment { get; set; }

    public Guid? FilterPartId { get; set; }

    public Guid? FilterSectionId { get; set; }

    public string? FilterOpNumber { get; set; }
}

public class AdminWipDeleteLabelInputModel
{
    [Required]
    public Guid LabelId { get; set; }

    public Guid? FilterPartId { get; set; }

    public Guid? FilterSectionId { get; set; }

    public string? FilterOpNumber { get; set; }
}
