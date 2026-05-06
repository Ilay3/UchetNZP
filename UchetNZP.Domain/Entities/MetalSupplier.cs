using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class MetalSupplier
{
    public Guid Id { get; set; }

    public string Identifier { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Inn { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<MetalReceipt> Receipts { get; set; } = new List<MetalReceipt>();
}
