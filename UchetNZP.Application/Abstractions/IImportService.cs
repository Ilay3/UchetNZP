using UchetNZP.Application.Contracts.Imports;

namespace UchetNZP.Application.Abstractions;

public interface IImportService
{
    Task<ImportSummaryDto> ImportRoutesExcelAsync(Stream stream, CancellationToken cancellationToken = default);
}
