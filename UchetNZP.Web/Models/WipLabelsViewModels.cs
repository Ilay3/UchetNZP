using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
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

public class WipLabelListResponseModel
{
    public WipLabelListResponseModel(
        IReadOnlyCollection<WipLabelListItemViewModel>? in_items,
        int in_page,
        int in_totalPages,
        int in_totalCount)
    {
        Items = in_items ?? Array.Empty<WipLabelListItemViewModel>();
        Page = Math.Max(1, in_page);
        TotalPages = Math.Max(0, in_totalPages);
        TotalCount = Math.Max(0, in_totalCount);
    }

    [JsonPropertyName("items")]
    public IReadOnlyCollection<WipLabelListItemViewModel> Items { get; }

    [JsonPropertyName("page")]
    public int Page { get; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; }
}
