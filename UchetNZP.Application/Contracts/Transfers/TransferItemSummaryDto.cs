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
    Guid TransferId,
    Guid TransferAuditId,
    Guid TransactionId,
    TransferScrapSummaryDto? Scrap,
    Guid? WipLabelId,
    string? LabelNumber,
    decimal? LabelQuantityBefore,
    decimal? LabelQuantityAfter,
    bool IsReverted
);
