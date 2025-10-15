namespace UchetNZP.Domain.Entities;

public class WipReceipt
{
    public Guid Id { get; set; }

    public Guid PartId { get; set; }

    public Guid SectionId { get; set; }

    public DateTime ReceiptDate { get; set; }

    public decimal Quantity { get; set; }

    public string? DocumentNumber { get; set; } // Ограничение длины -> Fluent API

    public virtual Part? Part { get; set; }

    public virtual Section? Section { get; set; }
}
