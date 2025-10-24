using UchetNZP.Domain.Entities;

namespace UchetNZP.Application.Contracts.Transfers;

public record TransferDeleteResultDto(
    Guid TransferId,
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
    bool IsWarehouseTransfer,
    IReadOnlyCollection<Guid> DeletedOperationIds,
    TransferDeleteScrapDto? Scrap,
    TransferDeleteWarehouseItemDto? WarehouseItem
);

public record TransferDeleteScrapDto(
    Guid ScrapId,
    ScrapType ScrapType,
    decimal Quantity,
    string? Comment
);

public record TransferDeleteWarehouseItemDto(
    Guid WarehouseItemId,
    decimal Quantity
);
