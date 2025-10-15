namespace UchetNZP.Domain.Entities;

public class WipBalance
{
    public Guid Id { get; set; }

    public Guid PartId { get; set; }

    public Guid SectionId { get; set; }

    public int OpNumber { get; set; }

    public decimal Quantity { get; set; }

    public virtual Part? Part { get; set; }

    public virtual Section? Section { get; set; }
}
