namespace UchetNZP.Application.Contracts.Wip;

public record ReceiptItemDto(
    Guid PartId,
    int OpNumber,
    Guid SectionId,
    DateTime ReceiptDate,
    decimal Quantity,
    string? DocumentNumber,
    string? Comment
);
