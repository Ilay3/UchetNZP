using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class WipLabel
{
    public Guid Id { get; set; }

    public Guid PartId { get; set; }

    public DateTime LabelDate { get; set; }

    public decimal Quantity { get; set; }

    public decimal RemainingQuantity { get; set; }

    public int LabelYear { get; set; }

    public string Number { get; set; } = string.Empty;

    public bool IsAssigned { get; set; }

    public virtual Part? Part { get; set; }

    public virtual WipReceipt? WipReceipt { get; set; }

    public virtual ICollection<WipTransfer> Transfers { get; set; } = new List<WipTransfer>();

    public virtual ICollection<WarehouseLabelItem> WarehouseLabelItems { get; set; } = new List<WarehouseLabelItem>();
}
