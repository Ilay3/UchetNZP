using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class MetalReceipt
{
    public Guid Id { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty;

    public DateTime ReceiptDate { get; set; }

    public string? SupplierOrSource { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<MetalReceiptItem> Items { get; set; } = new List<MetalReceiptItem>();
}
