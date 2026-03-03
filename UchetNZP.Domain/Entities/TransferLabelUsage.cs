namespace UchetNZP.Domain.Entities;

public class TransferLabelUsage
{
    public Guid Id { get; set; }

    public Guid TransferId { get; set; }

    public Guid FromLabelId { get; set; }

    public decimal Qty { get; set; }

    public decimal ScrapQty { get; set; }

    public decimal RemainingBefore { get; set; }

    public Guid? CreatedToLabelId { get; set; }

    public virtual WipTransfer? Transfer { get; set; }

    public virtual WipLabel? FromLabel { get; set; }

    public virtual WipLabel? CreatedToLabel { get; set; }
}
