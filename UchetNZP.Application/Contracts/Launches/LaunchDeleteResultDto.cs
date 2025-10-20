namespace UchetNZP.Application.Contracts.Launches;

public record LaunchDeleteResultDto(
    Guid LaunchId,
    Guid PartId,
    Guid SectionId,
    int FromOpNumber,
    decimal Remaining
);
