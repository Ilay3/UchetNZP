namespace UchetNZP.Application.Contracts.Admin;

public record AdminWipBulkCleanupRequestDto(
    Guid? PartId,
    Guid? SectionId,
    int? OpNumber,
    decimal MinQuantity,
    string? Comment);

public record AdminWipBulkCleanupPreviewDto(
    Guid JobId,
    int AffectedCount,
    decimal AffectedQuantity,
    Guid? PartId,
    Guid? SectionId,
    int? OpNumber,
    decimal MinQuantity);

public record AdminWipBulkCleanupExecuteDto(Guid JobId, bool Confirmed);

public record AdminWipBulkCleanupResultDto(
    Guid JobId,
    int UpdatedCount,
    decimal UpdatedQuantity,
    DateTime ExecutedAtUtc);
