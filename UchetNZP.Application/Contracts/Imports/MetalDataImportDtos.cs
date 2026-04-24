namespace UchetNZP.Application.Contracts.Imports;

public enum MetalImportMode
{
    Materials,
    Norms,
    All
}

public record MetalDataImportRequestDto(
    MetalImportMode Mode,
    bool DryRun,
    string SourceFileName
);

public record MetalDataImportErrorDto(int RowIndex, string Sheet, string Message);

public record MetalDataImportSummaryDto(
    string SourceFileName,
    bool DryRun,
    int MaterialsImported,
    int PartsFound,
    int PartsCreated,
    int NormsCreated,
    int NormsUpdated,
    int RowsSkipped,
    IReadOnlyList<MetalDataImportErrorDto> Errors,
    string? ErrorFileName,
    byte[]? ErrorFileContent
);
