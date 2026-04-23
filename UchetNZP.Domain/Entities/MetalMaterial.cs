using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class MetalMaterial
{
    public Guid Id { get; set; }

    public string? Code { get; set; }

    public string Name { get; set; } = string.Empty;

    public string UnitKind { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public virtual ICollection<MetalReceiptItem> ReceiptItems { get; set; } = new List<MetalReceiptItem>();
}
