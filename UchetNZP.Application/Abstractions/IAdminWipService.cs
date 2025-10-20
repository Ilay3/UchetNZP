using UchetNZP.Application.Contracts.Admin;

namespace UchetNZP.Application.Abstractions;

public interface IAdminWipService
{
    Task<AdminWipAdjustmentResultDto> AdjustBalanceAsync(AdminWipAdjustmentRequestDto request, CancellationToken cancellationToken = default);
}
