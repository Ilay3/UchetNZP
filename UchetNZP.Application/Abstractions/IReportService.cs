namespace UchetNZP.Application.Abstractions;

public interface IReportService
{
    Task<byte[]> ExportLaunchesToExcelAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    Task<byte[]> ExportLaunchesByDateAsync(DateTime date, CancellationToken cancellationToken = default);
}
