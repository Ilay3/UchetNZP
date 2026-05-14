using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace UchetNZP.Web.Models;

public class WarehouseIndexViewModel
{
    public Guid? SelectedPartId { get; init; }

    public string PartSearch { get; init; } = string.Empty;

    public string MovementFilter { get; init; } = "all";

    public IReadOnlyCollection<SelectListItem> Parts { get; init; } = Array.Empty<SelectListItem>();

    public IReadOnlyCollection<WarehouseItemRowViewModel> Items { get; init; } = Array.Empty<WarehouseItemRowViewModel>();

    public IReadOnlyCollection<WarehousePartGroupViewModel> PartGroups { get; init; } = Array.Empty<WarehousePartGroupViewModel>();

    public IReadOnlyCollection<WarehouseAreaViewModel> Areas { get; init; } = Array.Empty<WarehouseAreaViewModel>();

    public IReadOnlyCollection<WarehouseMovementTypeViewModel> MovementTypes { get; init; } = Array.Empty<WarehouseMovementTypeViewModel>();

    public IReadOnlyCollection<WarehouseMovementSourceViewModel> MovementSources { get; init; } = Array.Empty<WarehouseMovementSourceViewModel>();

    public WarehouseManualReceiptModel ManualReceipt { get; init; } = new();

    public WarehouseAssemblyUnitReceiptModel AssemblyUnitReceipt { get; init; } = new();

    public WarehouseManualIssueModel ManualIssue { get; init; } = new();

    public WarehouseAssemblyUnitIssueModel AssemblyUnitIssue { get; init; } = new();

    public decimal TotalQuantity { get; init; }

    public int AutomaticReceiptCount { get; init; }

    public int ManualReceiptCount { get; init; }

    public int ManualIssueCount { get; init; }

    public int TotalMovementCount { get; init; }

    public int ReceiptMovementCount { get; init; }

    public int IssueMovementCount { get; init; }

    public decimal ReceiptQuantity { get; init; }

    public decimal IssueQuantity { get; init; }

    public int BalanceGroupCount { get; init; }

    public string? AutoPrintControlCardUrl { get; init; }

    public string? StatusMessage { get; init; }

    public string? ErrorMessage { get; init; }

    public int CurrentPage { get; init; }

    public int PageSize { get; init; }

    public int TotalPages { get; init; }
}

public class WarehouseAreaViewModel
{
    public string Key { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public bool IsEnabled { get; init; }
}

public class WarehouseMovementTypeViewModel
{
    public string Title { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }
}

public class WarehouseMovementSourceViewModel
{
    public string Title { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }
}

public record WarehouseLabelLookupItemViewModel(
    Guid Id,
    string Number,
    decimal Quantity,
    decimal AvailableQuantity);

public record WarehouseAssemblyLabelLookupItemViewModel(
    string Number,
    decimal Quantity,
    decimal AvailableQuantity);

public class WarehouseItemRowViewModel
{
    public Guid Id { get; init; }

    public Guid? PartId { get; init; }

    public Guid? AssemblyUnitId { get; init; }

    public string ItemDisplay { get; init; } = string.Empty;

    public string ItemKindTitle { get; init; } = string.Empty;

    public Guid? TransferId { get; init; }

    public string MovementType { get; init; } = string.Empty;

    public string MovementTitle { get; init; } = string.Empty;

    public string SourceType { get; init; } = string.Empty;

    public string SourceTitle { get; init; } = string.Empty;

    public string? DocumentNumber { get; init; }

    public string? ControlCardNumber { get; init; }

    public string? ControllerName { get; init; }

    public string? MasterName { get; init; }

    public string? AcceptedByName { get; init; }

    public decimal Quantity { get; init; }

    public decimal QuantityImpact { get; init; }

    public DateTime AddedAt { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public string? Comment { get; init; }

    public IReadOnlyCollection<WarehouseLabelRowViewModel> LabelRows { get; init; } = Array.Empty<WarehouseLabelRowViewModel>();
}

public class WarehousePartGroupViewModel
{
    public Guid? PartId { get; init; }

    public Guid? AssemblyUnitId { get; init; }

    public string ItemDisplay { get; init; } = string.Empty;

    public string ItemKindTitle { get; init; } = string.Empty;

    public decimal TotalQuantity { get; init; }

    public decimal ReceiptQuantity { get; init; }

    public decimal IssueQuantity { get; init; }

    public int MovementCount { get; init; }

    public int ReceiptCount { get; init; }

    public int IssueCount { get; init; }

    public DateTime LastMovementAt { get; init; }

    public string LastMovementTitle { get; init; } = string.Empty;

    public string LastSourceTitle { get; init; } = string.Empty;

    public string? LastDocumentNumber { get; init; }

    public IReadOnlyCollection<WarehouseLabelGroupViewModel> LabelGroups { get; init; } = Array.Empty<WarehouseLabelGroupViewModel>();

    public IReadOnlyCollection<WarehouseItemRowViewModel> Items { get; init; } = Array.Empty<WarehouseItemRowViewModel>();
}

public class WarehouseLabelGroupViewModel
{
    public Guid? LabelId { get; init; }

    public string LabelNumber { get; init; } = string.Empty;

    public decimal TotalQuantity { get; init; }

    public DateTime FirstAddedAt { get; init; }

    public DateTime? LastUpdatedAt { get; init; }
}

public class WarehouseLabelRowViewModel
{
    public Guid? LabelId { get; init; }

    public string LabelNumber { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public DateTime AddedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }
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

    [MaxLength(64)]
    public string? DocumentNumber { get; set; }

    [MaxLength(64)]
    public string? ControlCardNumber { get; set; }

    [MaxLength(128)]
    public string? ControllerName { get; set; }

    [MaxLength(128)]
    public string? MasterName { get; set; }

    [MaxLength(128)]
    public string? AcceptedByName { get; set; }

    public Guid? FilterPartId { get; set; }

    [MaxLength(256)]
    public string? FilterPartSearch { get; set; }

    [MaxLength(16)]
    public string? FilterMovement { get; set; }

    public int? FilterPage { get; set; }

    public int? FilterPageSize { get; set; }
}

public class WarehouseManualReceiptModel
{
    [Required]
    public Guid PartId { get; set; }

    public string? PartSearch { get; set; }

    public Guid? WipLabelId { get; set; }

    [MaxLength(32)]
    public string? LabelNumber { get; set; }

    [Range(0.001, 999_999_999)]
    public decimal Quantity { get; set; }

    [DataType(DataType.Date)]
    public DateTime ReceiptDate { get; set; } = DateTime.Today;

    [MaxLength(64)]
    public string? DocumentNumber { get; set; }

    [MaxLength(64)]
    public string? ControlCardNumber { get; set; }

    [MaxLength(128)]
    public string? ControllerName { get; set; }

    [MaxLength(128)]
    public string? MasterName { get; set; }

    [MaxLength(128)]
    public string? AcceptedByName { get; set; }

    [MaxLength(512)]
    public string? Comment { get; set; }

    public bool PrintControlCard { get; set; } = true;
}

public class WarehouseAssemblyUnitReceiptModel
{
    public Guid? AssemblyUnitId { get; set; }

    [MaxLength(32)]
    public string? LabelNumber { get; set; }

    [Required]
    [MaxLength(256)]
    public string? AssemblyUnitName { get; set; }

    [Range(0.001, 999_999_999)]
    public decimal Quantity { get; set; }

    [DataType(DataType.Date)]
    public DateTime ReceiptDate { get; set; } = DateTime.Today;

    [MaxLength(64)]
    public string? DocumentNumber { get; set; }

    [MaxLength(64)]
    public string? ControlCardNumber { get; set; }

    [MaxLength(128)]
    public string? ControllerName { get; set; }

    [MaxLength(128)]
    public string? MasterName { get; set; }

    [MaxLength(128)]
    public string? AcceptedByName { get; set; }

    [MaxLength(512)]
    public string? Comment { get; set; }

    public bool PrintControlCard { get; set; } = true;
}

public class WarehouseManualIssueModel
{
    [Required]
    public Guid PartId { get; set; }

    public string? PartSearch { get; set; }

    public Guid? WipLabelId { get; set; }

    [MaxLength(32)]
    public string? LabelNumber { get; set; }

    [Range(0.001, 999_999_999)]
    public decimal Quantity { get; set; }

    [DataType(DataType.Date)]
    public DateTime IssueDate { get; set; } = DateTime.Today;

    [MaxLength(64)]
    public string? DocumentNumber { get; set; }

    [MaxLength(128)]
    public string? AcceptedByName { get; set; } = "Сборщик СИП отдел";

    [MaxLength(512)]
    public string? Comment { get; set; }
}

public class WarehouseAssemblyUnitIssueModel
{
    public Guid? AssemblyUnitId { get; set; }

    [MaxLength(32)]
    public string? LabelNumber { get; set; }

    [Required]
    [MaxLength(256)]
    public string? AssemblyUnitName { get; set; }

    [Range(0.001, 999_999_999)]
    public decimal Quantity { get; set; }

    [DataType(DataType.Date)]
    public DateTime IssueDate { get; set; } = DateTime.Today;

    [MaxLength(64)]
    public string? DocumentNumber { get; set; }

    [MaxLength(128)]
    public string? AcceptedByName { get; set; } = "Сборщик СИП отдел";

    [MaxLength(512)]
    public string? Comment { get; set; }
}
