using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class MetalReceipt
{
    public Guid Id { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty;

    public DateTime ReceiptDate { get; set; }

    public string? SupplierOrSource { get; set; }

    public string BatchNumber { get; set; } = string.Empty;

    public string? Comment { get; set; }

    public string? OriginalDocumentFileName { get; set; }

    public string? OriginalDocumentContentType { get; set; }

    public byte[]? OriginalDocumentContent { get; set; }

    public long? OriginalDocumentSizeBytes { get; set; }

    public DateTime? OriginalDocumentUploadedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<MetalReceiptItem> Items { get; set; } = new List<MetalReceiptItem>();
}
