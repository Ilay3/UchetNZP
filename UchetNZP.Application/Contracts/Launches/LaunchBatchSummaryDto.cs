using System.Collections.Generic;

namespace UchetNZP.Application.Contracts.Launches;

public record LaunchBatchSummaryDto(
    int Saved,
    IReadOnlyList<LaunchItemSummaryDto> Items
);
