using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace UchetNZP.Web.Models;

public class WarehouseIndexViewModel
{
    public Guid? SelectedPartId { get; init; }

    public IReadOnlyCollection<SelectListItem> Parts { get; init; } = Array.Empty<SelectListItem>();

    public IReadOnlyCollection<WarehouseItemRowViewModel> Items { get; init; } = Array.Empty<WarehouseItemRowViewModel>();

    public decimal TotalQuantity { get; init; }

    public string? StatusMessage { get; init; }

    public string? ErrorMessage { get; init; }
}

public class WarehouseItemRowViewModel
{
    public Guid Id { get; init; }

    public Guid PartId { get; init; }

    public string PartDisplay { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public DateTime AddedAt { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public string? Comment { get; init; }
}

public class WarehouseItemEditModel
{
    [Required]
    public Guid Id { get; set; }

    [Range(0, 999_999_999)]
    public decimal Quantity { get; set; }

    [DataType(DataType.Date)]
    public DateTime AddedAt { get; set; }

    [MaxLength(512)]
    public string? Comment { get; set; }

    public Guid? FilterPartId { get; set; }
}
