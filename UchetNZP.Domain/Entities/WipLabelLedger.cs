namespace UchetNZP.Domain.Entities;

public class WipLabelLedger
{
    public Guid EventId { get; set; }

    public DateTime EventTime { get; set; }

    public Guid UserId { get; set; }

    public Guid TransactionId { get; set; }

    public WipLabelEventType EventType { get; set; }

    public Guid? FromLabelId { get; set; }

    public Guid? ToLabelId { get; set; }

    public Guid? FromSectionId { get; set; }

    public int? FromOpNumber { get; set; }

    public Guid? ToSectionId { get; set; }

    public int? ToOpNumber { get; set; }

    public decimal Qty { get; set; }

    public decimal ScrapQty { get; set; }

    public string RefEntityType { get; set; } = string.Empty;

    public Guid? RefEntityId { get; set; }
}
