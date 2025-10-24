using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class WipTransfer
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid PartId { get; set; }

    public Guid FromSectionId { get; set; }

    public int FromOpNumber { get; set; }

    public Guid ToSectionId { get; set; }

    public int ToOpNumber { get; set; }

    public DateTime TransferDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public decimal Quantity { get; set; }

    public string? Comment { get; set; }

    public virtual Part? Part { get; set; }

    public virtual ICollection<WipTransferOperation> Operations { get; set; } = new List<WipTransferOperation>();

    public virtual WipScrap? Scrap { get; set; }

    public virtual WarehouseItem? WarehouseItem { get; set; }
}
