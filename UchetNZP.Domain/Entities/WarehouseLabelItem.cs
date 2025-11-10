namespace UchetNZP.Domain.Entities;

public class WarehouseLabelItem
{
    public Guid Id { get; set; }

    public Guid WarehouseItemId { get; set; }

    public Guid WipLabelId { get; set; }

    public decimal Quantity { get; set; }

    public DateTime AddedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual WarehouseItem? WarehouseItem { get; set; }

    public virtual WipLabel? WipLabel { get; set; }
}
