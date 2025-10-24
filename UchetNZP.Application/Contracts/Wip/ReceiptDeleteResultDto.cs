using System;

namespace UchetNZP.Application.Contracts.Wip;

public record ReceiptDeleteResultDto(
    Guid ReceiptId,
    Guid BalanceId,
    Guid PartId,
    Guid SectionId,
    int OpNumber,
    decimal ReceiptQuantity,
    decimal PreviousQuantity,
    decimal RestoredQuantity,
    decimal Delta
);
