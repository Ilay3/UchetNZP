using UchetNZP.Domain.Entities;

namespace UchetNZP.Application.Abstractions;

public interface IRouteService
{
    Task<IReadOnlyList<PartRoute>> GetRouteAsync(Guid partId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PartRoute>> GetTailToFinishAsync(Guid partId, string fromOpNumber, CancellationToken cancellationToken = default);

    Task<PartRoute> UpsertRouteAsync(
        string partName,
        string? partCode,
        string? operationName,
        int opNumber,
        decimal normHours,
        string sectionName,
        CancellationToken cancellationToken = default);
}
