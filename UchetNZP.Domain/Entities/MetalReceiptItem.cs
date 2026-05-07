using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class MetalReceiptItem
{
    public Guid Id { get; set; }

    public Guid MetalReceiptId { get; set; }

    public Guid MetalMaterialId { get; set; }

    public decimal PricePerKg { get; set; }

    public decimal Quantity { get; set; }

    public decimal TotalWeightKg { get; set; }

    public int ReceiptLineIndex { get; set; }

    public int ItemIndex { get; set; }

    public decimal SizeValue { get; set; }

    public string SizeUnitText { get; set; } = string.Empty;
    
    public string ActualBlankSizeText { get; set; } = string.Empty;

    public bool IsSizeApproximate { get; set; }

    public decimal PassportWeightKg { get; set; }

    public decimal ActualWeightKg { get; set; }

    public decimal CalculatedWeightKg { get; set; }

    public decimal WeightDeviationKg { get; set; }

    public string StockCategory { get; set; } = "whole";

    public string GeneratedCode { get; set; } = string.Empty;

    public bool IsConsumed { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public Guid? ConsumedByCuttingReportId { get; set; }

    public Guid? SourceCuttingReportId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual MetalReceipt? MetalReceipt { get; set; }

    public virtual MetalMaterial? MetalMaterial { get; set; }

    public virtual ICollection<MetalRequirementPlanItem> RequirementPlanItems { get; set; } = new List<MetalRequirementPlanItem>();

    public virtual ICollection<MetalIssueItem> IssueItems { get; set; } = new List<MetalIssueItem>();

    public virtual ICollection<MetalStockMovement> StockMovements { get; set; } = new List<MetalStockMovement>();
}
