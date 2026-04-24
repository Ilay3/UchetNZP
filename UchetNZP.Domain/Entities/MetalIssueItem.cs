namespace UchetNZP.Domain.Entities;

public class MetalIssueItem
{
    public Guid Id { get; set; }

    public Guid MetalIssueId { get; set; }

    public Guid MetalReceiptItemId { get; set; }

    public string SourceCode { get; set; } = string.Empty;

    public decimal SourceQtyBefore { get; set; }

    public decimal IssuedQty { get; set; }

    public decimal RemainingQtyAfter { get; set; }

    public string Unit { get; set; } = string.Empty;

    public string LineStatus { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public virtual MetalIssue? MetalIssue { get; set; }

    public virtual MetalReceiptItem? MetalReceiptItem { get; set; }
}
