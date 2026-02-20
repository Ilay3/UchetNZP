using UchetNZP.Application.Contracts.Admin;

namespace UchetNZP.Application.Abstractions;

public interface IAdminWipService
{
    Task<AdminWipAdjustmentResultDto> AdjustBalanceAsync(AdminWipAdjustmentRequestDto request, CancellationToken cancellationToken = default);

    Task<AdminWipBulkCleanupPreviewDto> PreviewBulkCleanupAsync(AdminWipBulkCleanupRequestDto request, CancellationToken cancellationToken = default);

    Task<AdminWipBulkCleanupResultDto> ExecuteBulkCleanupAsync(AdminWipBulkCleanupExecuteDto request, CancellationToken cancellationToken = default);
}
