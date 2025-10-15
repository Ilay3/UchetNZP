using UchetNZP.Application.Contracts.Imports;

namespace UchetNZP.Application.Abstractions;

public interface IImportService
{
    Task<ImportSummaryDto> ImportRoutesExcelAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);
}
