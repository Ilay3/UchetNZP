using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class MetalRequirement
{
    public Guid Id { get; set; }

    public string RequirementNumber { get; set; } = string.Empty;

    public DateTime RequirementDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public Guid? WipReceiptId { get; set; }

    public Guid WipLaunchId { get; set; }

    public Guid PartId { get; set; }

    public string PartCode { get; set; } = string.Empty;

    public string PartName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public Guid MetalMaterialId { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;

    public virtual WipLaunch? WipLaunch { get; set; }

    public virtual Part? Part { get; set; }

    public virtual MetalMaterial? MetalMaterial { get; set; }

    public virtual ICollection<MetalRequirementItem> Items { get; set; } = new List<MetalRequirementItem>();

    public virtual ICollection<CuttingPlan> CuttingPlans { get; set; } = new List<CuttingPlan>();

    public virtual ICollection<MetalRequirementPlan> RequirementPlans { get; set; } = new List<MetalRequirementPlan>();

    public virtual ICollection<MetalIssue> Issues { get; set; } = new List<MetalIssue>();
}
