using UchetNZP.Application.Contracts.Wip;

namespace UchetNZP.Application.Abstractions;

public interface IWipService
{
    Task<ReceiptBatchSummaryDto> AddReceiptsBatchAsync(IEnumerable<ReceiptItemDto> items, CancellationToken cancellationToken = default);
}
