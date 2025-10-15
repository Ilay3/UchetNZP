namespace UchetNZP.Application.Abstractions;

public interface IReportService
{
    Task<byte[]> ExportLaunchesToExcelAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
}
