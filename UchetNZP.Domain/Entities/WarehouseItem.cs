using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class WarehouseItem
{
    public Guid Id { get; set; }

    public Guid PartId { get; set; }

    public Guid? TransferId { get; set; }

    public decimal Quantity { get; set; }

    public DateTime AddedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? Comment { get; set; }

    public virtual Part? Part { get; set; }

    public virtual WipTransfer? Transfer { get; set; }

    public virtual ICollection<WarehouseLabelItem> WarehouseLabelItems { get; set; } = new List<WarehouseLabelItem>();
}
