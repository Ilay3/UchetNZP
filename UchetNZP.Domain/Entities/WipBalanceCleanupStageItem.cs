namespace UchetNZP.Domain.Entities;

public class WipBalanceCleanupStageItem
{
    public Guid Id { get; set; }

    public Guid JobId { get; set; }

    public Guid WipBalanceId { get; set; }

    public decimal PreviousQuantity { get; set; }

    public virtual WipBalanceCleanupJob? Job { get; set; }

    public virtual WipBalance? Balance { get; set; }
}
