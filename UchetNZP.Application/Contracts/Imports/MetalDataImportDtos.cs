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

public record MetalDataParsePreviewRowDto(
    int RowIndex,
    string Code,
    string? Name,
    string? SizeRaw,
    string ShapeType,
    decimal? DiameterMm,
    decimal? ThicknessMm,
    decimal? WidthMm,
    decimal? LengthMm,
    string UnitNorm,
    decimal? ValueNorm,
    string ParseStatus,
    string? ParseError);

public record MetalDataImportSummaryDto(
    string SourceFileName,
    bool DryRun,
    int MaterialsImported,
    int PartsFound,
    int PartsCreated,
    int NormsCreated,
    int NormsUpdated,
    int RowsSkipped,
    int ParsePreviewTotal,
    IReadOnlyList<MetalDataParsePreviewRowDto> ParsePreviewRows,
    IReadOnlyList<MetalDataImportErrorDto> Errors,
    string? ErrorFileName,
    byte[]? ErrorFileContent
);
