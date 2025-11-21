namespace UchetNZP.Domain.Entities;

public class ReceiptAudit
{
    public Guid Id { get; set; }

    public Guid VersionId { get; set; }

    public Guid ReceiptId { get; set; }

    public Guid PartId { get; set; }

    public Guid SectionId { get; set; }

    public int OpNumber { get; set; }

    public decimal? PreviousQuantity { get; set; }

    public decimal? NewQuantity { get; set; }

    public DateTime ReceiptDate { get; set; }

    public string? Comment { get; set; }

    public decimal PreviousBalance { get; set; }

    public decimal NewBalance { get; set; }

    public Guid? PreviousLabelId { get; set; }

    public Guid? NewLabelId { get; set; }

    public bool PreviousLabelAssigned { get; set; }

    public bool NewLabelAssigned { get; set; }

    public string Action { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public DateTime CreatedAt { get; set; }
}
