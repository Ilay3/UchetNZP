using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class MetalRequirement
{
    public Guid Id { get; set; }

    public string RequirementNumber { get; set; } = string.Empty;

    public DateTime RequirementDate { get; set; }

    public Guid WipLaunchId { get; set; }

    public Guid PartId { get; set; }

    public decimal Quantity { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string? Comment { get; set; }

    public virtual WipLaunch? WipLaunch { get; set; }

    public virtual Part? Part { get; set; }

    public virtual ICollection<MetalRequirementItem> Items { get; set; } = new List<MetalRequirementItem>();

    public virtual ICollection<CuttingPlan> CuttingPlans { get; set; } = new List<CuttingPlan>();
}
