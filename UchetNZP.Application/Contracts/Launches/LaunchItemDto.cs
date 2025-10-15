namespace UchetNZP.Application.Contracts.Launches;

public record LaunchItemDto(
    Guid PartId,
    int FromOpNumber,
    DateTime LaunchDate,
    decimal Quantity,
    string? DocumentNumber,
    string? Comment
);
