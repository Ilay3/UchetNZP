namespace UchetNZP.Application.Contracts.Imports;

public record ImportItemResultDto(
    int RowIndex,
    string Status,
    string? Message
);
