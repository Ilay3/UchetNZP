using UchetNZP.Domain.Entities;

namespace UchetNZP.Application.Contracts.Transfers;

public record TransferScrapDto(
    ScrapType ScrapType,
    decimal Quantity,
    string? Comment
);
