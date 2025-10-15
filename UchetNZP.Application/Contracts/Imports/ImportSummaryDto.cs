using System.Collections.Generic;

namespace UchetNZP.Application.Contracts.Imports;

public record ImportSummaryDto(
    Guid JobId,
    string FileName,
    int TotalRows,
    int Succeeded,
    int Skipped,
    IReadOnlyList<ImportItemResultDto> Items,
    string? ErrorFileName,
    byte[]? ErrorFileContent
);
