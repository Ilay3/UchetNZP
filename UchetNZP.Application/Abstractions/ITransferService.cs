using UchetNZP.Application.Contracts.Transfers;

namespace UchetNZP.Application.Abstractions;

public interface ITransferService
{
    Task<TransferBatchSummaryDto> AddTransfersBatchAsync(IEnumerable<TransferItemDto> items, CancellationToken cancellationToken = default);
}
