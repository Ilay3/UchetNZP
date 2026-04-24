namespace UchetNZP.Domain.Entities;

public class MetalRequirementPlanItem
{
    public Guid Id { get; set; }

    public Guid MetalRequirementPlanId { get; set; }

    public Guid? MetalReceiptItemId { get; set; }

    public string SourceCode { get; set; } = string.Empty;

    public decimal SourceSize { get; set; }

    public string SourceUnit { get; set; } = string.Empty;

    public decimal? SourceWeightKg { get; set; }

    public decimal PlannedUseQty { get; set; }

    public decimal RemainingAfterQty { get; set; }

    public string LineStatus { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public virtual MetalRequirementPlan? MetalRequirementPlan { get; set; }

    public virtual MetalReceiptItem? MetalReceiptItem { get; set; }
}
