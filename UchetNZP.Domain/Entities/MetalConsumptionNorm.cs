namespace UchetNZP.Domain.Entities;

public class MetalConsumptionNorm
{
    public Guid Id { get; set; }

    public Guid PartId { get; set; }

    public Guid MetalMaterialId { get; set; }

    public decimal ConsumptionQty { get; set; }

    public string ConsumptionUnit { get; set; } = string.Empty;

    public decimal? WeightPerUnitKg { get; set; }

    public string? Comment { get; set; }

    public bool IsActive { get; set; }

    public virtual Part? Part { get; set; }

    public virtual MetalMaterial? MetalMaterial { get; set; }
}
