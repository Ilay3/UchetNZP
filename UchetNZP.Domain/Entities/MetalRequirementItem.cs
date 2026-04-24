namespace UchetNZP.Domain.Entities;

public class MetalRequirementItem
{
    public Guid Id { get; set; }

    public Guid MetalRequirementId { get; set; }

    public Guid MetalMaterialId { get; set; }

    public decimal NormPerUnit { get; set; }

    public decimal TotalRequiredQty { get; set; }

    public string Unit { get; set; } = string.Empty;

    public decimal? TotalRequiredWeightKg { get; set; }

    public string? CalculationFormula { get; set; }

    public string? CalculationInput { get; set; }

    public string SelectionSource { get; set; } = string.Empty;

    public string? SelectionReason { get; set; }

    public string? CandidateMaterials { get; set; }

    public virtual MetalRequirement? MetalRequirement { get; set; }

    public virtual MetalMaterial? MetalMaterial { get; set; }
}
