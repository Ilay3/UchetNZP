namespace UchetNZP.Application.Contracts.Imports;

public record ImportItemResultDto(
    int RowNumber,
    string Status,
    string? Message
);
