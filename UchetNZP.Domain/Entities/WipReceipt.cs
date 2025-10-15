namespace UchetNZP.Domain.Entities;

public class WipReceipt
{
    public Guid Id { get; set; }

    public Guid PartId { get; set; }

    public Guid SectionId { get; set; }

    public int OpNumber { get; set; }

    public DateTime ReceiptDate { get; set; }

    public decimal Quantity { get; set; }

    public string? DocumentNumber { get; set; }

    public virtual Part? Part { get; set; }

    public virtual Section? Section { get; set; }
}
