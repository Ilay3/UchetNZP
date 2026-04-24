namespace UchetNZP.Application.Contracts.Wip;

public record ReceiptItemDto(
    Guid PartId,
    int OpNumber,
    Guid SectionId,
    Guid? MetalMaterialId,
    DateTime ReceiptDate,
    decimal Quantity,
    string? Comment,
    Guid? WipLabelId,
    string? LabelNumber,
    bool IsAssigned
);
