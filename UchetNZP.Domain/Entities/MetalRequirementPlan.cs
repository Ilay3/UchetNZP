using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class MetalRequirementPlan
{
    public Guid Id { get; set; }

    public Guid MetalRequirementId { get; set; }

    public string Status { get; set; } = string.Empty;

    public decimal BaseRequiredQty { get; set; }

    public decimal AdjustedRequiredQty { get; set; }

    public decimal PlannedQty { get; set; }

    public decimal DeficitQty { get; set; }

    public string? CalculationComment { get; set; }

    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime? RecalculatedAt { get; set; }

    public string? RecalculatedBy { get; set; }

    public virtual MetalRequirement? MetalRequirement { get; set; }

    public virtual ICollection<MetalRequirementPlanItem> Items { get; set; } = new List<MetalRequirementPlanItem>();
}
