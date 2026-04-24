namespace UchetNZP.Domain.Entities;

public class MetalStockMovement
{
    public Guid Id { get; set; }

    public DateTime MovementDate { get; set; }

    public string MovementType { get; set; } = string.Empty;

    public Guid MetalMaterialId { get; set; }

    public Guid? MetalReceiptItemId { get; set; }

    public string SourceDocumentType { get; set; } = string.Empty;

    public Guid SourceDocumentId { get; set; }

    public decimal? QtyBefore { get; set; }

    public decimal QtyChange { get; set; }

    public decimal? QtyAfter { get; set; }

    public string Unit { get; set; } = string.Empty;

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public virtual MetalMaterial? MetalMaterial { get; set; }

    public virtual MetalReceiptItem? MetalReceiptItem { get; set; }
}
