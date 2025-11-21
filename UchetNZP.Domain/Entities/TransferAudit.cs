using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class TransferAudit
{
    public Guid Id { get; set; }

    public Guid TransactionId { get; set; }

    public Guid TransferId { get; set; }

    public Guid PartId { get; set; }

    public Guid FromSectionId { get; set; }

    public int FromOpNumber { get; set; }

    public Guid ToSectionId { get; set; }

    public int ToOpNumber { get; set; }

    public decimal Quantity { get; set; }

    public string? Comment { get; set; }

    public DateTime TransferDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid UserId { get; set; }

    public decimal FromBalanceBefore { get; set; }

    public decimal FromBalanceAfter { get; set; }

    public decimal ToBalanceBefore { get; set; }

    public decimal ToBalanceAfter { get; set; }

    public bool IsWarehouseTransfer { get; set; }

    public Guid? WipLabelId { get; set; }

    public string? LabelNumber { get; set; }

    public decimal? LabelQuantityBefore { get; set; }

    public decimal? LabelQuantityAfter { get; set; }

    public decimal ScrapQuantity { get; set; }

    public ScrapType? ScrapType { get; set; }

    public string? ScrapComment { get; set; }

    public bool IsReverted { get; set; }

    public DateTime? RevertedAt { get; set; }

    public virtual ICollection<TransferAuditOperation> Operations { get; set; } = new List<TransferAuditOperation>();
}
