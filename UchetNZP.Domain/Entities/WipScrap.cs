namespace UchetNZP.Domain.Entities;

public class WipScrap
{
    public Guid Id { get; set; }

    public Guid PartId { get; set; }

    public Guid SectionId { get; set; }

    public int OpNumber { get; set; }

    public decimal Quantity { get; set; }

    public ScrapType ScrapType { get; set; }

    public DateTime RecordedAt { get; set; }

    public Guid UserId { get; set; }

    public string? Comment { get; set; }

    public Guid? TransferId { get; set; }

    public virtual Part? Part { get; set; }

    public virtual Section? Section { get; set; }

    public virtual WipTransfer? Transfer { get; set; }
}
