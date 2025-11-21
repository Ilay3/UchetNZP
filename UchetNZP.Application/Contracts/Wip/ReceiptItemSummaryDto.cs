namespace UchetNZP.Application.Contracts.Wip;

public record ReceiptItemSummaryDto(
    Guid PartId,
    int OpNumber,
    Guid SectionId,
    decimal Quantity,
    decimal Was,
    decimal Become,
    Guid BalanceId,
    Guid ReceiptId,
    Guid? WipLabelId,
    string? LabelNumber,
    bool IsAssigned,
    Guid VersionId
);
