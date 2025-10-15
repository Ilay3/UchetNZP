using UchetNZP.Application.Contracts.Launches;

namespace UchetNZP.Application.Abstractions;

public interface ILaunchService
{
    Task<LaunchBatchSummaryDto> AddLaunchesBatchAsync(IEnumerable<LaunchItemDto> items, CancellationToken cancellationToken = default);
}
