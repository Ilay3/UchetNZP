namespace UchetNZP.Domain.Entities;

public class WipBalanceCleanupJob
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ExecutedAt { get; set; }

    public Guid? PartId { get; set; }

    public Guid? SectionId { get; set; }

    public int? OpNumber { get; set; }

    public decimal MinQuantity { get; set; }

    public int AffectedCount { get; set; }

    public decimal AffectedQuantity { get; set; }

    public string? Comment { get; set; }

    public bool IsExecuted { get; set; }

    public virtual ICollection<WipBalanceCleanupStageItem> StageItems { get; set; } = new List<WipBalanceCleanupStageItem>();
}
