namespace UchetNZP.Domain.Entities;

public class MetalConsumptionNorm
{
    public Guid Id { get; set; }

    public Guid PartId { get; set; }

    public Guid? MetalMaterialId { get; set; }

    public string? SizeRaw { get; set; }

    public string NormalizedSizeRaw { get; set; } = string.Empty;

    public string NormKeyHash { get; set; } = string.Empty;

    public string? ConsumptionTextRaw { get; set; }

    public string ShapeType { get; set; } = "unknown";

    public decimal? DiameterMm { get; set; }

    public decimal? ThicknessMm { get; set; }

    public decimal? WidthMm { get; set; }

    public decimal? LengthMm { get; set; }

    public string UnitNorm { get; set; } = "pcs";

    public decimal? ValueNorm { get; set; }

    public string ParseStatus { get; set; } = "failed";

    public string? ParseError { get; set; }

    public decimal BaseConsumptionQty { get; set; }

    public string ConsumptionUnit { get; set; } = string.Empty;

    public string NormalizedConsumptionUnit { get; set; } = string.Empty;

    public string? SourceFile { get; set; }

    public string? Comment { get; set; }

    public bool IsActive { get; set; }

    public virtual Part? Part { get; set; }

    public virtual MetalMaterial? MetalMaterial { get; set; }
}
