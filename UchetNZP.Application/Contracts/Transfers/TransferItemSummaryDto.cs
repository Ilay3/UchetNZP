namespace UchetNZP.Application.Contracts.Transfers;

public record TransferItemSummaryDto(
    Guid PartId,
    int FromOpNumber,
    Guid FromSectionId,
    decimal FromBalanceBefore,
    decimal FromBalanceAfter,
    int ToOpNumber,
    Guid ToSectionId,
    decimal ToBalanceBefore,
    decimal ToBalanceAfter,
    decimal Quantity,
    Guid TransferId
);
