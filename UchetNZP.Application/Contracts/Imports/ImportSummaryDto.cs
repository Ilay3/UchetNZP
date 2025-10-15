using System.Collections.Generic;

namespace UchetNZP.Application.Contracts.Imports;

public record ImportSummaryDto(
    Guid JobId,
    int Processed,
    int Saved,
    int Skipped,
    IReadOnlyList<ImportItemResultDto> Items
);
