namespace UchetNZP.Application.Contracts.Wip;

public record ReceiptRevertResultDto(
    Guid ReceiptId,
    Guid BalanceId,
    Guid PartId,
    Guid SectionId,
    int OpNumber,
    decimal TargetQuantity,
    decimal PreviousQuantity,
    decimal NewQuantity,
    Guid VersionId);
