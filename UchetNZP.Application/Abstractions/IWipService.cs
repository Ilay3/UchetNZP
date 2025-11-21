using System;
using UchetNZP.Application.Contracts.Wip;

namespace UchetNZP.Application.Abstractions;

public interface IWipService
{
    Task<ReceiptBatchSummaryDto> AddReceiptsBatchAsync(IEnumerable<ReceiptItemDto> in_items, CancellationToken cancellationToken = default);

    Task<ReceiptDeleteResultDto> DeleteReceiptAsync(Guid in_receiptId, CancellationToken cancellationToken = default);

    Task<ReceiptRevertResultDto> RevertReceiptAsync(Guid in_receiptId, Guid in_versionId, CancellationToken cancellationToken = default);
}
