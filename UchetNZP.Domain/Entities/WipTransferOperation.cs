namespace UchetNZP.Domain.Entities;

public class WipTransferOperation
{
    public Guid Id { get; set; }

    public Guid WipTransferId { get; set; }

    public Guid OperationId { get; set; }

    public Guid SectionId { get; set; }

    public int OpNumber { get; set; }

    public Guid? PartRouteId { get; set; }

    public decimal QuantityChange { get; set; }

    public virtual WipTransfer? WipTransfer { get; set; }

    public virtual Operation? Operation { get; set; }

    public virtual Section? Section { get; set; }

    public virtual PartRoute? PartRoute { get; set; }
}
