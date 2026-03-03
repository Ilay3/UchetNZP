using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class WipLabel
{
    public Guid Id { get; set; }

    public Guid PartId { get; set; }

    public DateTime LabelDate { get; set; }

    public decimal Quantity { get; set; }

    public decimal RemainingQuantity { get; set; }

    public string Number { get; set; } = string.Empty;

    public bool IsAssigned { get; set; }

    public WipLabelStatus Status { get; set; } = WipLabelStatus.Active;

    public Guid? CurrentSectionId { get; set; }

    public int? CurrentOpNumber { get; set; }

    public Guid RootLabelId { get; set; }

    public Guid? ParentLabelId { get; set; }

    public string RootNumber { get; set; } = string.Empty;

    public int Suffix { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual Part? Part { get; set; }

    public virtual WipReceipt? WipReceipt { get; set; }

    public virtual ICollection<WipTransfer> Transfers { get; set; } = new List<WipTransfer>();

    public virtual ICollection<TransferLabelUsage> TransferUsagesAsSource { get; set; } = new List<TransferLabelUsage>();

    public virtual ICollection<TransferLabelUsage> TransferUsagesAsCreated { get; set; } = new List<TransferLabelUsage>();

    public virtual ICollection<LabelMerge> MergeOutputs { get; set; } = new List<LabelMerge>();

    public virtual ICollection<LabelMerge> MergeInputs { get; set; } = new List<LabelMerge>();

    public virtual ICollection<WarehouseLabelItem> WarehouseLabelItems { get; set; } = new List<WarehouseLabelItem>();
}
