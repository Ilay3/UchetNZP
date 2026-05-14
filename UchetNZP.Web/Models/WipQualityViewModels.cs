namespace UchetNZP.Web.Models;

public record WipQualityIndexViewModel(IReadOnlyList<WipQualityQueueItemViewModel> Items)
{
    public bool HasItems => Items.Count > 0;
}

public record WipQualityQueueItemViewModel(
    Guid PartId,
    string PartName,
    string? PartCode,
    Guid SectionId,
    string SectionName,
    string OpNumber,
    string OperationName,
    decimal Quantity,
    IReadOnlyList<WipQualityLabelViewModel> Labels,
    string DefectLabelPreview,
    string ReturnOperationHint);

public record WipQualityLabelViewModel(
    Guid Id,
    string Number,
    string RootNumber,
    decimal RemainingQuantity);
