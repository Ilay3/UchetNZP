namespace UchetNZP.Domain.Entities;

public class TransferAuditOperation
{
    public Guid Id { get; set; }

    public Guid TransferAuditId { get; set; }

    public Guid SectionId { get; set; }

    public int OpNumber { get; set; }

    public Guid? OperationId { get; set; }

    public Guid? PartRouteId { get; set; }

    public decimal BalanceBefore { get; set; }

    public decimal BalanceAfter { get; set; }

    public decimal QuantityChange { get; set; }

    public bool IsWarehouse { get; set; }

    public virtual TransferAudit? TransferAudit { get; set; }
}
