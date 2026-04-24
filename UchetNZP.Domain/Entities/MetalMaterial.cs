using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class MetalMaterial
{
    public Guid Id { get; set; }

    public string? Code { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal MassPerMeterKg { get; set; }

    public decimal MassPerSquareMeterKg { get; set; }

    public decimal CoefConsumption { get; set; } = 1m;

    public string StockUnit { get; set; } = "pcs";

    public decimal? WeightPerUnitKg { get; set; }

    public decimal Coefficient { get; set; } = 1m;

    public string? DisplayName { get; set; }

    public string UnitKind { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public virtual ICollection<MetalReceiptItem> ReceiptItems { get; set; } = new List<MetalReceiptItem>();

    public virtual ICollection<MetalConsumptionNorm> ConsumptionNorms { get; set; } = new List<MetalConsumptionNorm>();

    public virtual ICollection<MetalRequirementItem> RequirementItems { get; set; } = new List<MetalRequirementItem>();

    public virtual ICollection<MetalRequirement> Requirements { get; set; } = new List<MetalRequirement>();

    public virtual ICollection<MetalStockMovement> StockMovements { get; set; } = new List<MetalStockMovement>();
}
