using System;
using UchetNZP.Shared;

namespace UchetNZP.Application.Contracts.Wip;

public class WipLabelDto
{
    public WipLabelDto(
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

public class WipLabelFilterDto
{
    public WipLabelFilterDto(DateTime? in_from, DateTime? in_to, Guid? in_partId)
    {
        From = in_from;
        To = in_to;
        PartId = in_partId;
    }

    public DateTime? From { get; }

    public DateTime? To { get; }

    public Guid? PartId { get; }
}

public class WipLabelCreateDto
{
    public WipLabelCreateDto(Guid in_partId, DateTime in_labelDate, decimal in_quantity)
    {
        PartId = in_partId;
        LabelDate = DateTime.SpecifyKind(in_labelDate, DateTimeKind.Unspecified);
        Quantity = in_quantity;
    }

    public Guid PartId { get; }

    public DateTime LabelDate { get; }

    public decimal Quantity { get; }
}

public class WipLabelBatchCreateDto
{
    public WipLabelBatchCreateDto(Guid in_partId, DateTime in_labelDate, decimal in_quantity, int in_count)
    {
        PartId = in_partId;
        LabelDate = DateTime.SpecifyKind(in_labelDate, DateTimeKind.Unspecified);
        Quantity = in_quantity;
        Count = in_count;
    }

    public Guid PartId { get; }

    public DateTime LabelDate { get; }

    public decimal Quantity { get; }

    public int Count { get; }
}

public class WipLabelManualCreateDto
{
    public WipLabelManualCreateDto(Guid in_partId, DateTime in_labelDate, decimal in_quantity, string in_number)
    {
        PartId = in_partId;
        LabelDate = DateTime.SpecifyKind(in_labelDate, DateTimeKind.Unspecified);
        Quantity = in_quantity;
        Number = in_number ?? string.Empty;
    }

    public Guid PartId { get; }

    public DateTime LabelDate { get; }

    public decimal Quantity { get; }

    public string Number { get; }
}

public class WipLabelUpdateDto
{
    public WipLabelUpdateDto(Guid in_id, DateTime in_labelDate, decimal in_quantity, string in_number)
    {
        Id = in_id;
        LabelDate = DateTime.SpecifyKind(in_labelDate, DateTimeKind.Unspecified);
        Quantity = in_quantity;
        Number = in_number ?? string.Empty;
    }

    public Guid Id { get; }

    public DateTime LabelDate { get; }

    public decimal Quantity { get; }

    public string Number { get; }
}
