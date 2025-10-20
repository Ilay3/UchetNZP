using System.Collections.Generic;

namespace UchetNZP.Application.Contracts.Transfers;

public record TransferBatchSummaryDto(
    int Saved,
    IReadOnlyList<TransferItemSummaryDto> Items
);
