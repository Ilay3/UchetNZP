namespace UchetNZP.Domain.Entities;

public class WipLabel
{
    public Guid Id { get; set; }

    public Guid PartId { get; set; }

    public DateTime LabelDate { get; set; }

    public decimal Quantity { get; set; }

    public string Number { get; set; } = string.Empty;

    public bool IsAssigned { get; set; }

    public virtual Part? Part { get; set; }

    public virtual WipReceipt? WipReceipt { get; set; }
}
