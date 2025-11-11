using UchetNZP.Application.Contracts.Launches;

namespace UchetNZP.Application.Abstractions;

public interface IReportService
{
    Task<byte[]> ExportLaunchesToExcelAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    Task<byte[]> ExportLaunchesByDateAsync(DateTime date, CancellationToken cancellationToken = default);

    Task<byte[]> ExportLaunchCartAsync(IReadOnlyList<LaunchItemDto> items, CancellationToken cancellationToken = default);

    Task<byte[]> ExportRoutesToExcelAsync(string? search, Guid? sectionId, CancellationToken cancellationToken = default);
}
