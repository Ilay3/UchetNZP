using UchetNZP.Application.Contracts.Cutting;

namespace UchetNZP.Application.Abstractions;

public interface ICuttingPlanService
{
    Task<CuttingPlanResultDto> BuildAndSaveAsync(SaveCuttingPlanRequest request, CancellationToken cancellationToken = default);
}
