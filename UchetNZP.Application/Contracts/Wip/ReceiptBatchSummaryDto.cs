using System.Collections.Generic;

namespace UchetNZP.Application.Contracts.Wip;

public record ReceiptBatchSummaryDto(
    int Saved,
    IReadOnlyList<ReceiptItemSummaryDto> Items
);
