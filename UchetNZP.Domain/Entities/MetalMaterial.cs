using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class MetalMaterial
{
    public Guid Id { get; set; }

    public string? Code { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal? WeightPerUnitKg { get; set; }

    public decimal Coefficient { get; set; } = 1m;

    public string? DisplayName { get; set; }

    public string UnitKind { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public virtual ICollection<MetalReceiptItem> ReceiptItems { get; set; } = new List<MetalReceiptItem>();

    public virtual ICollection<MetalConsumptionNorm> ConsumptionNorms { get; set; } = new List<MetalConsumptionNorm>();

    public virtual ICollection<MetalRequirementItem> RequirementItems { get; set; } = new List<MetalRequirementItem>();
}
