using System;
using UchetNZP.Shared;

namespace UchetNZP.Web.Models;

public class WipLabelsPageViewModel
{
    public WipLabelsPageViewModel(DateTime in_defaultDate)
    {
        DefaultDate = DateTime.SpecifyKind(in_defaultDate.Date, DateTimeKind.Unspecified);
    }

    public DateTime DefaultDate { get; }
}

public class WipLabelListItemViewModel
{
    public WipLabelListItemViewModel(
        Guid in_id,
        Guid in_partId,
        string in_partName,
        string? in_partCode,
        string in_number,
        DateTime in_labelDate,
        decimal in_quantity,
        bool in_isAssigned)
    {
        Id = in_id;
        PartId = in_partId;
        PartName = in_partName ?? string.Empty;
        PartCode = in_partCode;
        Number = in_number ?? string.Empty;
        LabelDate = DateTime.SpecifyKind(in_labelDate, DateTimeKind.Unspecified);
        Quantity = in_quantity;
        IsAssigned = in_isAssigned;
    }

    public Guid Id { get; }

    public Guid PartId { get; }

    public string PartName { get; }

    public string? PartCode { get; }

    public string Number { get; }

    public DateTime LabelDate { get; }

    public decimal Quantity { get; }

    public bool IsAssigned { get; }

    public string PartDisplayName => NameWithCodeFormatter.getNameWithCode(PartName, PartCode);
}

public class WipLabelCreateInputModel
{
    public Guid PartId { get; set; }

    public DateTime LabelDate { get; set; }

    public decimal Quantity { get; set; }
}

public class WipLabelBatchInputModel : WipLabelCreateInputModel
{
    public int Count { get; set; }
}

public class WipLabelManualCreateInputModel : WipLabelCreateInputModel
{
    public string Number { get; set; } = string.Empty;
}

public class WipLabelUpdateInputModel
{
    public Guid Id { get; set; }

    public DateTime LabelDate { get; set; }

    public decimal Quantity { get; set; }

    public string Number { get; set; } = string.Empty;
}
