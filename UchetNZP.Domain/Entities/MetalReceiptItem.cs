namespace UchetNZP.Domain.Entities;

public class MetalReceiptItem
{
    public Guid Id { get; set; }

    public Guid MetalReceiptId { get; set; }

    public Guid MetalMaterialId { get; set; }

    public decimal Quantity { get; set; }

    public decimal TotalWeightKg { get; set; }

    public int ItemIndex { get; set; }

    public decimal SizeValue { get; set; }

    public string SizeUnitText { get; set; } = string.Empty;

    public string GeneratedCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public virtual MetalReceipt? MetalReceipt { get; set; }

    public virtual MetalMaterial? MetalMaterial { get; set; }
}
