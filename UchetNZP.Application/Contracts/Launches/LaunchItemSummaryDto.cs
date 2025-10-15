namespace UchetNZP.Application.Contracts.Launches;

public record LaunchItemSummaryDto(
    Guid PartId,
    int FromOpNumber,
    Guid SectionId,
    decimal Quantity,
    decimal Remaining,
    decimal SumHoursToFinish,
    Guid LaunchId
);
