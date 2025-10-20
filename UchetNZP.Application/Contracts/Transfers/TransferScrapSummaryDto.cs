using UchetNZP.Domain.Entities;

namespace UchetNZP.Application.Contracts.Transfers;

public record TransferScrapSummaryDto(
    Guid ScrapId,
    ScrapType ScrapType,
    decimal Quantity,
    string? Comment
);
