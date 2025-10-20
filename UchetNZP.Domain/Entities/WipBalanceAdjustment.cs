namespace UchetNZP.Domain.Entities;

public class WipBalanceAdjustment
{
    public Guid Id { get; set; }

    public Guid WipBalanceId { get; set; }

    public Guid PartId { get; set; }

    public Guid SectionId { get; set; }

    public int OpNumber { get; set; }

    public decimal PreviousQuantity { get; set; }

    public decimal NewQuantity { get; set; }

    public decimal Delta { get; set; }

    public string? Comment { get; set; }

    public Guid UserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual WipBalance? Balance { get; set; }

    public virtual Part? Part { get; set; }

    public virtual Section? Section { get; set; }
}
