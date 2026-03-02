namespace UchetNZP.Application.Contracts.Transfers;

public record TransferItemDto(
    Guid PartId,
    int FromOpNumber,
    int ToOpNumber,
    DateTime TransferDate,
    decimal Quantity,
    string? Comment,
    Guid? WipLabelId,
    bool CreateResidualLabel,
    int? ResidualLabelNumber,
    TransferScrapDto? Scrap
);
